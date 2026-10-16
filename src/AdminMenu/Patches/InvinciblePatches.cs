using HarmonyLib;
using Trash;

namespace AdminMenu.Patches
{
    /// <summary>
    /// The menu's own invincibility. The game has equivalent flags on TrailerCommands, but that component is
    /// editor-only and may be missing from release scenes, so the menu doesn't rely on it.
    /// </summary>
    internal static class Invincibility
    {
        /// <summary>Set around the menu's own Kill so it still works while players are invincible.</summary>
        internal static bool Bypass;

        internal static bool PlayersBlocked => Plugin.InvinciblePlayers.Value && !Bypass;
        internal static bool CartBlocked => Plugin.InvincibleCart.Value;
    }

    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.TakeDamage))]
    internal static class InvinciblePlayersPatch
    {
        private static bool Prefix() => !Invincibility.PlayersBlocked;
    }

    // Breaks cart panels and, with none left, ends the run.
    [HarmonyPatch(typeof(ShoppingCartController), nameof(ShoppingCartController.TakeDamage))]
    internal static class InvincibleCartPatch
    {
        private static bool Prefix() => !Invincibility.CartBlocked;
    }

    // Loosens wheel bolts from hazards.
    [HarmonyPatch(typeof(ShoppingCartWheel), nameof(ShoppingCartWheel.TakeDamage))]
    internal static class InvincibleWheelPatch
    {
        private static bool Prefix() => !Invincibility.CartBlocked;
    }

    // Loosens wheel bolts from hard landings and collisions.
    [HarmonyPatch(typeof(ShoppingCartWheel), nameof(ShoppingCartWheel.TakeImpactDamage))]
    internal static class InvincibleWheelImpactPatch
    {
        private static bool Prefix() => !Invincibility.CartBlocked;
    }
}
