using System;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>Level tab: skip ahead, finish the level, or jump to one of the level's named teleport points.</summary>
    internal static class LevelTab
    {
        public static VisualElement Build(AdminMenuPanel panel, out Action refresh)
        {
            var root = new VisualElement();

            var flow = Ui.Row();
            flow.Add(Ui.ActionButton(panel, "Next checkpoint", GameActions.NextCheckpoint));
            flow.Add(Ui.ConfirmButton(panel, "Complete level", GameActions.CompleteLevel));
            root.Add(flow);

            root.Add(Ui.Heading("Teleport points"));
            root.Add(Ui.Muted("Moves every player and the cart."));
            var points = new ScrollView();
            points.style.flexGrow = 1f;
            root.Add(points);

            refresh = () =>
            {
                points.Clear();
                var names = GameActions.TeleportPoints();
                if (names.Count == 0)
                {
                    points.Add(Ui.Muted("No teleport points in this level."));
                    return;
                }
                var row = Ui.Row();
                foreach (var name in names)
                {
                    var target = name;
                    var button = Ui.ActionButton(panel, target, () => GameActions.TeleportTo(target));
                    button.style.marginBottom = 6f;
                    row.Add(button);
                }
                Theme.StyleControls(row);
                points.Add(row);
            };
            return root;
        }
    }
}
