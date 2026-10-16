using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>
    /// Players tab: the invincibility toggle and one row per connected player. Polls once a second while the menu
    /// is open and rebuilds only when someone joins, leaves, dies or respawns.
    /// </summary>
    internal static class PlayersTab
    {
        public static VisualElement Build(AdminMenuPanel panel, out Action refresh)
        {
            var root = new VisualElement();

            var invincible = Ui.InvincibleToggle(panel, "Invincible (everyone)", Plugin.InvinciblePlayers,
                "Players are invincible", "Players take damage again");
            root.Add(invincible);

            var list = new ScrollView();
            list.style.flexGrow = 1f;
            root.Add(list);

            string shown = null;

            void Render(bool force)
            {
                var players = GameActions.Players();
                var signature = string.Join("|", players.Select(p => $"{p.ClientId}:{p.Name}:{p.HasBody}:{p.IsDead}"));
                if (!force && signature == shown)
                    return;
                shown = signature;
                list.Clear();
                if (players.Count == 0)
                {
                    list.Add(Ui.Muted("No players."));
                    return;
                }
                foreach (var player in players)
                {
                    var row = BuildRow(panel, player, () => Render(true));
                    // Rows are new elements, so styling them here can't stack hover callbacks.
                    Theme.StyleControls(row);
                    list.Add(row);
                }
            }

            root.schedule.Execute(() =>
            {
                if (!panel.IsOpen)
                    return;
                try { Render(false); }
                catch (Exception e) { Plugin.Log.LogWarning($"Players tab refresh failed: {e.Message}"); }
            }).Every(1000L);

            refresh = () =>
            {
                invincible.SetValueWithoutNotify(Plugin.InvinciblePlayers.Value);
                Render(true);
            };
            return root;
        }

        private static VisualElement BuildRow(AdminMenuPanel panel, PlayerInfo player, Action rerender)
        {
            var row = Ui.Row();
            row.style.paddingBottom = 10f;
            row.style.borderBottomWidth = 1f;
            row.style.borderBottomColor = Theme.Line;

            var name = new Label(player.IsLocal ? $"{player.Name} (you)" : player.Name);
            name.style.color = Theme.Text;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.width = 200f;
            row.Add(name);

            var state = new Label(!player.HasBody ? "No character" : player.IsDead ? "Dead" : "Alive");
            state.style.color = player.IsDead ? Ui.Error : Theme.Muted;
            state.style.width = 110f;
            row.Add(state);

            var id = player.ClientId;
            var revive = Ui.ActionButton(panel, "Revive", () => GameActions.Revive(id), rerender);
            revive.SetEnabled(player.HasBody && player.IsDead);
            row.Add(revive);

            var heal = Ui.ActionButton(panel, "Heal", () => GameActions.Heal(id), rerender);
            heal.SetEnabled(player.HasBody && !player.IsDead);
            row.Add(heal);

            var kill = Ui.ActionButton(panel, "Kill", () => GameActions.Kill(id), rerender);
            kill.SetEnabled(player.HasBody && !player.IsDead);
            row.Add(kill);

            var bring = Ui.ActionButton(panel, "Bring to me", () => GameActions.BringToMe(id));
            bring.SetEnabled(player.HasBody && !player.IsLocal);
            row.Add(bring);

            var goTo = Ui.ActionButton(panel, "Go to", () => GameActions.GoTo(id));
            goTo.SetEnabled(player.HasBody && !player.IsLocal);
            row.Add(goTo);

            return row;
        }
    }
}
