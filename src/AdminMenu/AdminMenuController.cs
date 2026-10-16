using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ox1;
using QFSW.QC;
using Trash;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace AdminMenu
{
    /// <summary>
    /// Owns the menu: reads the hotkey, opens and closes the window, and takes the game's input while it's open
    /// the same way the game's own console does (see Trash.ConsoleMenuHider): gameplay actions off, game menus
    /// suppressed so Esc can't open the pause menu underneath, plus a look block and a free cursor.
    /// </summary>
    internal sealed class AdminMenuController : MonoBehaviour
    {
        private enum CloseReason
        {
            /// <summary>Close button or hotkey.</summary>
            User,
            /// <summary>Esc: keep game menus suppressed until the key is released.</summary>
            Escape,
            /// <summary>Hosting ended or the player is gone: hand input back but leave the cursor to the game.</summary>
            Lost,
        }

        private UIDocument _document;
        private AdminMenuPanel _panel;
        private bool _open;
        /// <summary>True when we turned MenusSuppressed on, so we never clear one the console set.</summary>
        private bool _suppressedMenus;
        private bool _holdSuppressionUntilEscReleased;
        private bool _loggedTickError;

        private void Update()
        {
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                if (!_loggedTickError)
                {
                    _loggedTickError = true;
                    Plugin.Log.LogError($"Admin menu update failed; closing the menu. {e}");
                }
                if (_open)
                    Close(CloseReason.Lost);
            }
        }

        private void OnDestroy()
        {
            if (_open)
                Close(CloseReason.Lost);
            ReleaseSuppression();
        }

        private void Tick()
        {
            if (_holdSuppressionUntilEscReleased && !Hotkey.EscapeHeld())
                ReleaseSuppression();

            if (_open)
            {
                if (_panel.Root.panel == null || MenuGate.ShouldAutoClose(GameActions.IsServer, GameActions.LocalPlayer() != null))
                {
                    Close(CloseReason.Lost);
                    return;
                }
                if (Plugin.CloseOnEscape.Value && Hotkey.EscapePressed())
                {
                    Close(CloseReason.Escape);
                    return;
                }
                KeepInputLocked();
            }

            if (Hotkey.WasPressed(Plugin.ToggleKey.Value))
                Toggle();
        }

        private void Toggle()
        {
            var decision = MenuGate.Decide(_open, GameActions.IsServer, GameActions.LocalPlayer() != null, MenuSceneManager.IsMenuOpen, ConsoleOpen());
            switch (decision)
            {
                case ToggleDecision.Open:
                    Open();
                    break;
                case ToggleDecision.Close:
                    Close(CloseReason.User);
                    break;
                default:
                    var message = MenuGate.Message(decision);
                    if (message != null)
                        GameActions.Toast(message);
                    break;
            }
        }

        private void Open()
        {
            if (!EnsurePanel())
            {
                Plugin.Log.LogWarning("No game UI to borrow panel settings from yet; the admin menu can't open here.");
                return;
            }

            _open = true;
            _holdSuppressionUntilEscReleased = false;
            if (!MenuSceneManager.MenusSuppressed)
            {
                MenuSceneManager.MenusSuppressed = true;
                _suppressedMenus = true;
            }
            PlayerCamera.LookInputBlocks.SetRequest(this, true);
            KeepInputLocked();
            _panel.Open();
        }

        private void Close(CloseReason reason)
        {
            if (!_open)
                return;
            _open = false;
            _panel?.Close();

            PlayerCamera.LookInputBlocks.SetRequest(this, false);
            if (MenuGate.ShouldEnableGameInput(MenuSceneManager.AnyMenuVisible.Value, ConsoleOpen()))
                InputSystem.actions?.Enable();
            if (reason != CloseReason.Lost && MenuGate.ShouldLockCursor(MenuSceneManager.IsMenuOpen))
                Cursor.lockState = CursorLockMode.Locked;

            if (reason == CloseReason.Escape)
                _holdSuppressionUntilEscReleased = true;
            else
                ReleaseSuppression();
        }

        /// <summary>Re-asserted every frame: a game menu closing under ours re-enables actions and relocks the cursor.</summary>
        private static void KeepInputLocked()
        {
            var actions = InputSystem.actions;
            if (actions != null && actions.enabled)
                actions.Disable();
            if (Cursor.lockState != CursorLockMode.None)
                Cursor.lockState = CursorLockMode.None;
        }

        private void ReleaseSuppression()
        {
            _holdSuppressionUntilEscReleased = false;
            // A console opened over the menu now owns the suppression and clears it when it closes.
            if (MenuGate.ShouldClearMenuSuppression(_suppressedMenus, ConsoleOpen()))
                MenuSceneManager.MenusSuppressed = false;
            _suppressedMenus = false;
        }

        /// <summary>
        /// Whether the game's developer console (launched with -console) is open. It turns gameplay input off and
        /// suppresses menus itself, so closing the admin menu must leave both alone while it's open.
        /// </summary>
        private static bool ConsoleOpen()
        {
            try
            {
                return ConsoleOpenCore();
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Separate and non-inlined so a missing console assembly fails inside ConsoleOpen's try.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool ConsoleOpenCore()
        {
            var console = QuantumConsole.Instance;
            return console != null && console.IsActive;
        }

        /// <summary>
        /// Builds the window once, in its own UIDocument that survives scene loads. Its PanelSettings are new screen-overlay
        /// settings carrying a game document's theme, fonts and scaling, drawn above everything else.
        /// </summary>
        private bool EnsurePanel()
        {
            if (_document == null)
            {
                var settings = CreatePanelSettings();
                if (settings == null)
                    return false;
                var go = new GameObject("AdminMenuUI");
                go.SetActive(false);
                go.transform.SetParent(transform, false);
                _document = go.AddComponent<UIDocument>();
                _document.panelSettings = settings;
                go.SetActive(true);
            }

            if (_panel == null)
            {
                _panel = new AdminMenuPanel();
                _panel.CloseRequested += () => Close(CloseReason.User);
                _panel.AddTab("Players", PlayersTab.Build(_panel, out var refreshPlayers), refreshPlayers);
                _panel.AddTab("Items", ItemsTab.Build(_panel, out var refreshItems), refreshItems);
                _panel.AddTab("Cart", CartTab.Build(_panel, out var refreshCart), refreshCart);
                _panel.AddTab("Level", LevelTab.Build(_panel, out var refreshLevel), refreshLevel);
            }

            var root = _document.rootVisualElement;
            if (root == null)
                return false;
            if (_panel.Root.parent != root)
            {
                _panel.Root.RemoveFromHierarchy();
                root.Add(_panel.Root);
            }
            return _panel.Root.panel != null;
        }

        /// <summary>
        /// Fresh settings rather than a copy, so a world-space or screen-clearing source panel can't carry over. Only
        /// the look (theme, fonts) and scaling are borrowed, preferably from the toast document (see <see cref="PanelSource"/>).
        /// </summary>
        private static PanelSettings CreatePanelSettings()
        {
            var documents = new List<UIDocument>();
            UIDocument toastDocument = null;
            if (ToastUI.Instance.TryGet(out var toast) && toast != null)
            {
                toastDocument = toast.GetComponentInChildren<UIDocument>(true);
                if (toastDocument != null)
                    documents.Add(toastDocument);
            }
            foreach (var document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (document != null && document != toastDocument)
                    documents.Add(document);
            }

            var candidates = new List<PanelCandidate>();
            foreach (var document in documents)
            {
                var source = document.panelSettings;
                candidates.Add(new PanelCandidate
                {
                    IsToast = document == toastDocument,
                    HasTheme = source != null && source.themeStyleSheet != null,
                    HasTargetTexture = source != null && source.targetTexture != null,
                });
            }
            var index = PanelSource.Pick(candidates);
            if (index < 0)
                return null;

            var from = documents[index].panelSettings;
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "AdminMenuPanelSettings";
            settings.themeStyleSheet = from.themeStyleSheet;
            settings.textSettings = from.textSettings;
            settings.scaleMode = from.scaleMode;
            settings.referenceResolution = from.referenceResolution;
            settings.screenMatchMode = from.screenMatchMode;
            settings.match = from.match;
            settings.scale = from.scale;
            settings.referenceDpi = from.referenceDpi;
            settings.fallbackDpi = from.fallbackDpi;
            settings.clearColor = false;
            settings.clearDepthStencil = false;
            settings.sortingOrder = 10000f;
            return settings;
        }
    }
}
