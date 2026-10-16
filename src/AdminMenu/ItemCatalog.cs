using System;
using System.Collections.Generic;
using Ox1;
using Trash;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AdminMenu
{
    /// <summary>
    /// Every network prefab with an <see cref="Item"/> component, named the way the game names it when you look
    /// at it. Built on first use and rebuilt when the network manager or its prefab list changes.
    /// </summary>
    internal static class ItemCatalog
    {
        private static readonly Dictionary<ItemEntry, GameObject> Prefabs = new Dictionary<ItemEntry, GameObject>();
        private static List<ItemEntry> _entries = new List<ItemEntry>();
        private static NetworkManager _builtFor;
        private static int _builtCount = -1;

        public static IReadOnlyList<ItemEntry> All()
        {
            var manager = NetworkManager.Singleton;
            var prefabs = manager != null ? manager.NetworkConfig?.Prefabs?.Prefabs : null;
            if (prefabs == null)
                return Array.Empty<ItemEntry>();
            if (manager == _builtFor && prefabs.Count == _builtCount)
                return _entries;

            Prefabs.Clear();
            var entries = new List<ItemEntry>();
            foreach (var networkPrefab in prefabs)
            {
                var prefab = networkPrefab?.Prefab;
                if (prefab == null)
                    continue;
                try
                {
                    if (prefab.GetComponent<Item>() == null)
                        continue;
                    var entry = new ItemEntry { Name = DisplayName(prefab), Rarity = Rarity(prefab), PrefabName = prefab.name };
                    entries.Add(entry);
                    Prefabs[entry] = prefab;
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Skipping item prefab {prefab.name}: {e.Message}");
                }
            }

            _entries = entries;
            _builtFor = manager;
            _builtCount = prefabs.Count;
            Plugin.Log.LogInfo($"Item catalog: {entries.Count} items");
            return _entries;
        }

        /// <summary>Spawns <paramref name="count"/> copies a couple of metres in front of the host, slightly scattered.</summary>
        public static string Spawn(ItemEntry entry, int count)
        {
            if (entry == null || !Prefabs.TryGetValue(entry, out var prefab) || prefab == null)
                throw new InvalidOperationException("that item is no longer available; reopen the menu");
            if (!GameActions.IsServer)
                throw new InvalidOperationException("only the host can spawn items");

            var origin = GameActions.SpawnPoint(2f) + Vector3.up * 0.3f;
            for (var i = 0; i < count; i++)
            {
                var offset = new Vector3(Random.Range(-0.3f, 0.3f), i * 0.15f, Random.Range(-0.3f, 0.3f));
                ItemSpawnManager.InstantiateRuntimeItem(prefab, origin + offset, Quaternion.identity);
            }
            return count == 1 ? $"Spawned {entry.Name}" : $"Spawned {count} x {entry.Name}";
        }

        /// <summary>The localized name the pickup prompt shows, or the prefab's name when it has none.</summary>
        private static string DisplayName(GameObject prefab)
        {
            var text = prefab.GetComponentInChildren<PickupInteractionText>(true);
            var key = text != null ? text.NameKey : null;
            if (string.IsNullOrEmpty(key))
                return prefab.name;
            var localized = Loc.Localize(key).Text;
            return string.IsNullOrWhiteSpace(localized) || localized == key ? prefab.name : localized;
        }

        private static string Rarity(GameObject prefab)
        {
            var loot = prefab.GetComponent<Loot>();
            return loot != null ? loot.Rarity.ToString() : string.Empty;
        }
    }
}
