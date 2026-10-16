using System;
using UnityEngine.UIElements;

namespace AdminMenu
{
    /// <summary>Cart tab: where the cart is and what state it's in, plus the fixes for a stuck or broken cart.</summary>
    internal static class CartTab
    {
        public static VisualElement Build(AdminMenuPanel panel, out Action refresh)
        {
            var root = new VisualElement();

            var invincible = Ui.InvincibleToggle(panel, "Invincible cart", Plugin.InvincibleCart,
                "The cart is invincible", "The cart takes damage again");
            root.Add(invincible);

            var summary = Ui.Muted(string.Empty);
            root.Add(summary);

            void UpdateSummary() => summary.text = GameActions.CartSummary();

            var buttons = Ui.Row();
            buttons.Add(Ui.ActionButton(panel, "Bring cart to me", GameActions.BringCart, UpdateSummary));
            buttons.Add(Ui.ActionButton(panel, "Flip upright", GameActions.FlipCart, UpdateSummary));
            buttons.Add(Ui.ActionButton(panel, "Repair", GameActions.RepairCart, UpdateSummary));
            root.Add(buttons);

            root.Add(Ui.Muted("Repair puts every broken panel back at tier 1 and clears cart damage. It doesn't tighten wheel bolts."));

            refresh = () =>
            {
                invincible.SetValueWithoutNotify(Plugin.InvincibleCart.Value);
                UpdateSummary();
            };
            return root;
        }
    }
}
