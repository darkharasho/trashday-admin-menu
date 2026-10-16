using System;
using System.Collections.Generic;
using System.Linq;

namespace AdminMenu
{
    /// <summary>One spawnable item, kept free of game types so the search can be tested without the game.</summary>
    internal sealed class ItemEntry
    {
        public string Name;
        public string Rarity;
        /// <summary>The network prefab's name; a stable tiebreak for items that share a display name.</summary>
        public string PrefabName;
    }

    /// <summary>Filters the item list by display name.</summary>
    internal static class ItemSearch
    {
        public static List<ItemEntry> Filter(IEnumerable<ItemEntry> items, string query)
        {
            var term = (query ?? string.Empty).Trim();
            return (items ?? Enumerable.Empty<ItemEntry>())
                .Where(i => i != null && (term.Length == 0 || Contains(i.Name, term)))
                .OrderBy(i => i.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.PrefabName ?? string.Empty, StringComparer.Ordinal)
                .ToList();
        }

        private static bool Contains(string text, string term) =>
            text != null && text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
