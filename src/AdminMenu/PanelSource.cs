using System.Collections.Generic;

namespace AdminMenu
{
    /// <summary>What the controller knows about one of the game's UI documents, kept free of Unity types for tests.</summary>
    internal struct PanelCandidate
    {
        /// <summary>The toast document: always a screen overlay, present in every level.</summary>
        public bool IsToast;
        public bool HasTheme;
        public bool HasTargetTexture;
    }

    /// <summary>
    /// Picks the game document whose theme and text settings the menu borrows. Only those (and the scaling) are
    /// copied into fresh panel settings, so a world-space or full-screen-clearing source can't break the menu, but the
    /// toast's are preferred because they're known to be a screen overlay.
    /// </summary>
    internal static class PanelSource
    {
        /// <returns>The index of the candidate to use, or -1 if none has a theme and draws to the screen.</returns>
        public static int Pick(IReadOnlyList<PanelCandidate> candidates)
        {
            if (candidates == null)
                return -1;
            var fallback = -1;
            for (var i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (!c.HasTheme || c.HasTargetTexture)
                    continue;
                if (c.IsToast)
                    return i;
                if (fallback < 0)
                    fallback = i;
            }
            return fallback;
        }
    }
}
