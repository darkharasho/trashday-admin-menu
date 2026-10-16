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
        /// <summary>A game menu such as pause, or the developer console, is open: do nothing.</summary>
        GameMenuOpen,
    }

    /// <summary>
    /// When the menu may open, when it must close itself, and what to hand back to the game on close. Closing is
    /// always allowed, so a scene change or a lost host can never leave the menu stuck open.
    /// </summary>
    internal static class MenuGate
    {
        public static ToggleDecision Decide(bool menuOpen, bool isServer, bool hasLocalPlayer, bool gameMenuOpen, bool consoleOpen)
        {
            if (menuOpen)
                return ToggleDecision.Close;
            if (!hasLocalPlayer)
                return ToggleDecision.NotInLevel;
            if (!isServer)
                return ToggleDecision.HostOnly;
            if (gameMenuOpen || consoleOpen)
                return ToggleDecision.GameMenuOpen;
            return ToggleDecision.Open;
        }

        public static string Message(ToggleDecision decision) =>
            decision == ToggleDecision.HostOnly ? "Admin menu: host only" : null;

        public static bool ShouldAutoClose(bool isServer, bool hasLocalPlayer) => !isServer || !hasLocalPlayer;

        /// <summary>
        /// Mirrors the game's console: gameplay input comes back only if no game menu wants it off. The console turns
        /// input off itself and doesn't show up as a game menu, so it's checked separately.
        /// </summary>
        public static bool ShouldEnableGameInput(bool gameMenuVisible, bool consoleOpen) => !gameMenuVisible && !consoleOpen;

        /// <summary>
        /// Only clear the game's menu suppression if this menu set it, and never under an open console that relies on it
        /// to keep Esc from opening the pause menu.
        /// </summary>
        public static bool ShouldClearMenuSuppression(bool suppressedByUs, bool consoleOpen) => suppressedByUs && !consoleOpen;

        /// <summary>A game menu that opened over ours needs the free cursor it already has.</summary>
        public static bool ShouldLockCursor(bool gameMenuOpen) => !gameMenuOpen;
    }
}
