using System;
using Ox1;
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
            var decision = MenuGate.Decide(_open, GameActions.IsServer, GameActions.LocalPlayer() != null, MenuSceneManager.IsMenuOpen);
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
            if (MenuGate.ShouldEnableGameInput(MenuSceneManager.AnyMenuVisible.Value))
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
            if (!_suppressedMenus)
                return;
            _suppressedMenus = false;
            MenuSceneManager.MenusSuppressed = false;
        }

        /// <summary>
        /// Builds the window once, in its own UIDocument that survives scene loads. Its PanelSettings are a copy of a
        /// game document's, so the game's theme and fonts carry over, drawn above everything else.
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

        private static PanelSettings CreatePanelSettings()
        {
            foreach (var document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                var source = document != null ? document.panelSettings : null;
                if (source == null || source.targetTexture != null || source.themeStyleSheet == null)
                    continue;
                var settings = Instantiate(source);
                settings.name = "AdminMenuPanelSettings";
                settings.sortingOrder = 10000f;
                return settings;
            }
            return null;
        }
    }
}
