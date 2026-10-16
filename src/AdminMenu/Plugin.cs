using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace AdminMenu
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.darkharasho.trashday.adminmenu";
        public const string PluginName = "AdminMenu";
        public const string PluginVersion = "0.1.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey;
        internal static ConfigEntry<bool> CloseOnEscape;
        internal static ConfigEntry<bool> InvinciblePlayers;
        internal static ConfigEntry<bool> InvincibleCart;

        /// <summary>False when patching failed; the invincibility toggles are then disabled.</summary>
        internal static bool PatchesApplied { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            ToggleKey = Config.Bind("General", "ToggleKey", new KeyboardShortcut(KeyCode.F1), new ConfigDescription(
                "Opens and closes the admin menu. Only the host can open it.",
                null,
                new ConfigurationManagerAttributes { DispName = "Toggle key", Order = 4 }));
            CloseOnEscape = Config.Bind("General", "CloseOnEscape", true, new ConfigDescription(
                "Esc closes the admin menu (without opening the pause menu).",
                null,
                new ConfigurationManagerAttributes { DispName = "Close on Esc", Order = 3 }));
            InvinciblePlayers = Config.Bind("Cheats", "InvinciblePlayers", false, new ConfigDescription(
                "Players take no damage. Also on the menu's Players tab.",
                null,
                new ConfigurationManagerAttributes { DispName = "Invincible players", Order = 2 }));
            InvincibleCart = Config.Bind("Cheats", "InvincibleCart", false, new ConfigDescription(
                "The cart, its panels and its wheels take no damage. Also on the menu's Cart tab.",
                null,
                new ConfigurationManagerAttributes { DispName = "Invincible cart", Order = 1 }));

            _harmony = new Harmony(PluginGuid);
            try
            {
                _harmony.PatchAll();
                PatchesApplied = true;
            }
            catch (Exception e)
            {
                Log.LogError($"Patching failed, likely a game update; invincibility is unavailable, the rest of the menu still works. {e}");
                _harmony.UnpatchSelf();
            }

            // Its own object, so it outlives scene loads and doesn't depend on BepInEx's manager object staying alive.
            var host = new GameObject("AdminMenu");
            DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<AdminMenuController>();

            Log.LogInfo($"{PluginName} {PluginVersion} loaded; press {ToggleKey.Value} while hosting to open the admin menu");
        }

        private void OnDestroy() => _harmony?.UnpatchSelf();
    }
}
