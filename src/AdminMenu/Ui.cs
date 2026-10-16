using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>Small building blocks the tabs share.</summary>
    internal static class Ui
    {
        public static readonly Color Error = new Color(1f, 0.45f, 0.4f);

        /// <summary>A themed button that runs <paramref name="work"/> through the panel's status line, then <paramref name="after"/>.</summary>
        public static Button ActionButton(AdminMenuPanel panel, string text, Func<string> work, Action after = null)
        {
            var button = new Button(() =>
            {
                panel.Run(text, work);
                after?.Invoke();
            }) { text = text };
            Theme.StyleButton(button);
            button.style.marginRight = 6f;
            return button;
        }

        /// <summary>Like <see cref="ActionButton"/>, but the first click only arms it for three seconds.</summary>
        public static Button ConfirmButton(AdminMenuPanel panel, string text, Func<string> work)
        {
            Button button = null;
            var armed = false;
            button = new Button(() =>
            {
                if (!armed)
                {
                    armed = true;
                    button.text = $"Click again: {text}";
                    button.schedule.Execute(() => { armed = false; button.text = text; }).ExecuteLater(3000L);
                    return;
                }
                armed = false;
                button.text = text;
                panel.Run(text, work);
            }) { text = text };
            Theme.StyleButton(button);
            button.style.marginRight = 6f;
            return button;
        }

        /// <summary>
        /// A toggle bound to a cheat config entry. Disabled with a note when patching failed, because the flag would
        /// do nothing.
        /// </summary>
        public static Toggle InvincibleToggle(AdminMenuPanel panel, string label, BepInEx.Configuration.ConfigEntry<bool> entry, string onText, string offText)
        {
            var toggle = new Toggle(label) { value = entry.Value };
            toggle.style.marginBottom = 12f;
            if (!Plugin.PatchesApplied)
            {
                toggle.label = $"{label} (unavailable: patching failed, see the BepInEx log)";
                toggle.SetEnabled(false);
                return toggle;
            }
            toggle.RegisterValueChangedCallback(evt =>
            {
                entry.Value = evt.newValue;
                panel.SetStatus(evt.newValue ? onText : offText, false);
            });
            return toggle;
        }

        public static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginBottom = 10f;
            return row;
        }

        public static Label Heading(string text)
        {
            var label = new Label(text);
            label.style.fontSize = 20f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = Theme.Gold;
            label.style.marginTop = 8f;
            label.style.marginBottom = 8f;
            return label;
        }

        public static Label Muted(string text)
        {
            var label = new Label(text);
            label.style.color = Theme.Muted;
            label.style.marginBottom = 8f;
            return label;
        }
    }
}
