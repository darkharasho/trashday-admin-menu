namespace AdminMenu
{
    /// <summary>What pressing the toggle key should do.</summary>
    internal enum ToggleDecision
    {
        Open,
        Close,
        /// <summary>A client in a level: tell them the menu is for the host.</summary>
        HostOnly,
        /// <summary>No local player (main menu, loading): do nothing.</summary>
        NotInLevel,
        /// <summary>A game menu such as pause is open: do nothing.</summary>
        GameMenuOpen,
    }

    /// <summary>
    /// When the menu may open, when it must close itself, and what to hand back to the game on close. Closing is
    /// always allowed, so a scene change or a lost host can never leave the menu stuck open.
    /// </summary>
    internal static class MenuGate
    {
        public static ToggleDecision Decide(bool menuOpen, bool isServer, bool hasLocalPlayer, bool gameMenuOpen)
        {
            if (menuOpen)
                return ToggleDecision.Close;
            if (!hasLocalPlayer)
                return ToggleDecision.NotInLevel;
            if (!isServer)
                return ToggleDecision.HostOnly;
            if (gameMenuOpen)
                return ToggleDecision.GameMenuOpen;
            return ToggleDecision.Open;
        }

        public static string Message(ToggleDecision decision) =>
            decision == ToggleDecision.HostOnly ? "Admin menu: host only" : null;

        public static bool ShouldAutoClose(bool isServer, bool hasLocalPlayer) => !isServer || !hasLocalPlayer;

        /// <summary>Mirrors the game's console: gameplay input comes back only if no game menu wants it off.</summary>
        public static bool ShouldEnableGameInput(bool gameMenuVisible) => !gameMenuVisible;

        /// <summary>A game menu that opened over ours needs the free cursor it already has.</summary>
        public static bool ShouldLockCursor(bool gameMenuOpen) => !gameMenuOpen;
    }
}
