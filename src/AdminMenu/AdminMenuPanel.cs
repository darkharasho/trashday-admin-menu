using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>
    /// The admin menu window: a rail of tab buttons on the left, the selected tab on the right, and a status line
    /// at the bottom that shows how the last action went. Keyboard handling (the hotkey and Esc) lives in
    /// <see cref="AdminMenuController"/>.
    /// </summary>
    internal sealed class AdminMenuPanel
    {
        private readonly VisualElement _overlay;
        private readonly VisualElement _rail;
        private readonly VisualElement _content;
        private readonly Label _status;
        private readonly List<Tab> _tabs = new List<Tab>();

        private Tab _selected;

        public event Action CloseRequested;

        public VisualElement Root => _overlay;
        public bool IsOpen { get; private set; }

        public AdminMenuPanel()
        {
            _overlay = new VisualElement { name = "AdminMenuOverlay" };
            var o = _overlay.style;
            o.position = Position.Absolute;
            o.left = o.right = o.top = o.bottom = 0f;
            o.alignItems = Align.Center;
            o.justifyContent = Justify.Center;
            o.backgroundColor = new Color(0f, 0f, 0f, 0.7f);
            o.color = Theme.Text;
            o.display = DisplayStyle.None;

            var window = new VisualElement { name = "AdminMenuWindow" };
            var w = window.style;
            w.width = Length.Percent(72f);
            w.height = Length.Percent(78f);
            w.maxWidth = 1200f;
            w.backgroundColor = Theme.Window;
            Theme.SetBorder(window, 3f, Theme.WindowBorder, 0f);
            Theme.SetPadding(window, 20f, 24f);
            _overlay.Add(window);

            var header = Row();
            header.style.marginBottom = 16f;
            var title = new Label("ADMIN MENU");
            title.style.fontSize = 34f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Theme.Text;
            title.style.textShadow = new TextShadow { offset = new Vector2(2f, 2f), color = Color.black };
            title.style.flexGrow = 1f;
            header.Add(title);

            var close = new Button(() => CloseRequested?.Invoke()) { text = "Close" };
            close.style.marginLeft = 20f;
            close.style.minWidth = 110f;
            Theme.StyleButton(close);
            header.Add(close);
            window.Add(header);

            var body = Row();
            body.style.flexGrow = 1f;
            body.style.flexShrink = 1f;
            body.style.alignItems = Align.Stretch;
            window.Add(body);

            _rail = new VisualElement { name = "AdminMenuTabs" };
            _rail.style.width = 180f;
            _rail.style.flexShrink = 0f;
            _rail.style.marginRight = 20f;
            _rail.style.paddingRight = 16f;
            _rail.style.borderRightWidth = 2f;
            _rail.style.borderRightColor = Theme.Line;
            body.Add(_rail);

            _content = new VisualElement { name = "AdminMenuContent" };
            _content.style.flexGrow = 1f;
            _content.style.flexShrink = 1f;
            body.Add(_content);

            _status = new Label(" ") { name = "AdminMenuStatus" };
            _status.style.marginTop = 12f;
            _status.style.paddingTop = 10f;
            _status.style.borderTopWidth = 2f;
            _status.style.borderTopColor = Theme.Line;
            _status.style.color = Theme.Muted;
            _status.style.whiteSpace = WhiteSpace.Normal;
            window.Add(_status);

            Theme.StyleControls(_overlay);
        }

        /// <summary>
        /// Adds a tab and its content. The first tab added becomes the selected one. The content is styled once,
        /// here: a tab that rebuilds its own children later must call <see cref="Theme.StyleControls"/> on what it
        /// rebuilt, because styling registers hover callbacks and re-running it would stack them up.
        /// </summary>
        public void AddTab(string title, VisualElement content, Action onShown)
        {
            var button = new Button { text = title };
            Theme.StyleButton(button, outlined: false);
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.marginLeft = button.style.marginRight = 0f;
            button.style.marginBottom = 8f;
            button.style.paddingTop = button.style.paddingBottom = 10f;
            _rail.Add(button);

            content.style.display = DisplayStyle.None;
            content.style.flexGrow = 1f;
            _content.Add(content);
            Theme.StyleControls(content);

            var tab = new Tab { Button = button, Content = content, OnShown = onShown };
            _tabs.Add(tab);
            button.clicked += () => Select(tab);

            if (_selected == null)
                Select(tab);
            else
                Highlight();
        }

        public void Open()
        {
            _overlay.style.display = DisplayStyle.Flex;
            _overlay.BringToFront();
            IsOpen = true;
            SetStatus(" ", false);
            // Refresh the visible tab so it never shows data from the last time the menu was open.
            if (_selected != null)
                Show(_selected);
        }

        public void Close()
        {
            IsOpen = false;
            _overlay.style.display = DisplayStyle.None;
        }

        public void SetStatus(string text, bool error)
        {
            _status.text = string.IsNullOrEmpty(text) ? " " : text;
            _status.style.color = error ? Ui.Error : Theme.Muted;
        }

        /// <summary>
        /// Runs one button's work. Its message, or "action failed: reason", goes to the status line, so a game update
        /// that breaks one action leaves the rest of the menu working.
        /// </summary>
        public void Run(string action, Func<string> work)
        {
            try
            {
                SetStatus(work() ?? action, false);
            }
            catch (InvalidOperationException e)
            {
                SetStatus($"{action} failed: {e.Message}", true);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"{action} failed: {e}");
                SetStatus($"{action} failed: {e.Message}", true);
            }
        }

        private void Select(Tab tab)
        {
            _selected = tab;
            foreach (var other in _tabs)
                other.Content.style.display = other == tab ? DisplayStyle.Flex : DisplayStyle.None;
            Highlight();
            Show(tab);
        }

        /// <summary>Gold outlines the tab in view; the rest keep the plain line border.</summary>
        private void Highlight()
        {
            foreach (var tab in _tabs)
                Theme.SetBorder(tab.Button, 2f, tab == _selected ? Theme.Gold : Theme.Line, 4f);
        }

        private void Show(Tab tab)
        {
            if (tab.OnShown == null)
                return;
            Run(tab.Button.text, () =>
            {
                tab.OnShown();
                return _status.text;
            });
        }

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            return row;
        }

        private sealed class Tab
        {
            public Button Button;
            public VisualElement Content;
            public Action OnShown;
        }
    }
}
