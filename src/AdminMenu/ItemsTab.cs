using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>
    /// Items tab: team money at the top, then a searchable item list with Spawn 1/5/10 on each row. The list is
    /// rebuilt on every keystroke, so each row is styled as it's created.
    /// </summary>
    internal static class ItemsTab
    {
        private const int MaxResults = 200;

        public static VisualElement Build(AdminMenuPanel panel, out Action refresh)
        {
            var all = new List<ItemEntry>();
            var root = new VisualElement();

            root.Add(Ui.Heading("Money"));
            var moneyRow = Ui.Row();
            var money = new Label();
            money.style.color = Theme.Text;
            money.style.width = 200f;
            moneyRow.Add(money);

            void UpdateMoney()
            {
                var value = GameActions.Money();
                money.text = value.HasValue ? $"Team money: ${value.Value:N0}" : "Team money: n/a";
            }

            foreach (var amount in new[] { 10f, 100f, 1000f })
            {
                var add = amount;
                moneyRow.Add(Ui.ActionButton(panel, $"+${add:N0}", () => GameActions.AddMoney(add), UpdateMoney));
            }

            var custom = new TextField { value = string.Empty };
            custom.style.width = 120f;
            custom.style.marginLeft = 12f;
            moneyRow.Add(custom);
            moneyRow.Add(Ui.ActionButton(panel, "Add", () =>
            {
                if (!Amounts.TryParseMoney(custom.value, out var amount))
                    throw new InvalidOperationException($"enter an amount between 1 and {Amounts.MaxMoney:N0}");
                return GameActions.AddMoney(amount);
            }, UpdateMoney));
            root.Add(moneyRow);

            root.Add(Ui.Heading("Items"));
            var searchContainer = new VisualElement();
            searchContainer.style.marginBottom = 12f;
            var search = new TextField { value = string.Empty };
            searchContainer.Add(search);
            var placeholder = new Label("Search items…");
            placeholder.style.color = Theme.Muted;
            placeholder.style.position = Position.Absolute;
            placeholder.style.left = 8f;
            placeholder.style.top = 0f;
            placeholder.style.bottom = 0f;
            placeholder.style.unityTextAlign = TextAnchor.MiddleLeft;
            placeholder.pickingMode = PickingMode.Ignore;
            searchContainer.Add(placeholder);
            root.Add(searchContainer);

            var results = new ScrollView();
            results.style.flexGrow = 1f;
            root.Add(results);

            void RenderResults()
            {
                results.Clear();
                var matches = ItemSearch.Filter(all, search.value);
                if (matches.Count == 0)
                {
                    results.Add(Ui.Muted(all.Count == 0 ? "No items found in this session." : "No items match."));
                    return;
                }
                var shown = 0;
                foreach (var item in matches)
                {
                    if (shown++ >= MaxResults)
                    {
                        results.Add(Ui.Muted("… more results; refine the search"));
                        break;
                    }
                    var row = BuildRow(panel, item);
                    Theme.StyleControls(row);
                    results.Add(row);
                }
            }

            search.RegisterValueChangedCallback(evt =>
            {
                placeholder.style.display = string.IsNullOrEmpty(evt.newValue) ? DisplayStyle.Flex : DisplayStyle.None;
                RenderResults();
            });

            refresh = () =>
            {
                UpdateMoney();
                all.Clear();
                all.AddRange(ItemCatalog.All());
                RenderResults();
            };
            return root;
        }

        private static VisualElement BuildRow(AdminMenuPanel panel, ItemEntry item)
        {
            var row = Ui.Row();
            row.style.paddingBottom = 8f;
            row.style.borderBottomWidth = 1f;
            row.style.borderBottomColor = Theme.Line;

            var name = new Label(item.Name);
            name.style.color = Theme.Text;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.flexGrow = 1f;
            name.style.width = 220f;
            row.Add(name);

            var rarity = new Label(item.Rarity);
            rarity.style.color = Theme.Muted;
            rarity.style.width = 100f;
            row.Add(rarity);

            foreach (var count in new[] { 1, 5, 10 })
            {
                var n = count;
                row.Add(Ui.ActionButton(panel, $"Spawn {n}", () => ItemCatalog.Spawn(item, n)));
            }
            return row;
        }
    }
}
