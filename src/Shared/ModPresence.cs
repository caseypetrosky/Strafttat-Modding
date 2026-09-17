// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx.Bootstrap;

namespace StraftatModding
{
    /// <summary>
    /// Which other STRAFTAT mods are loaded, so a plugin can cooperate with them
    /// instead of quietly fighting them.
    /// </summary>
    /// <remarks>
    /// Read from BepInEx's own plugin registry by GUID. Presence is checked once
    /// the chainloader has finished, so this is only meaningful from Awake
    /// onwards - which is when it is used.
    /// </remarks>
    internal static class ModPresence
    {
        /// <summary>moreStrafts: raises the lobby cap. GPL-3.0, source published.</summary>
        public const string MoreStraftsGuid = "com.nitrogenia.morestrafts";

        /// <summary>
        /// MoreStrafts UI and Spawn Fixes Addon: rebuilds the 5-10 player lobby
        /// UI and applies its own circular spawn offsets.
        /// </summary>
        public const string UiSpawnAddonGuid = "com.morestrafts.uispawn.addon";

        public static bool MoreStrafts => IsLoaded(MoreStraftsGuid);
        public static bool UiSpawnAddon => IsLoaded(UiSpawnAddonGuid);

        /// <summary>True when a plugin with this GUID is loaded.</summary>
        public static bool IsLoaded(string guid)
        {
            try
            {
                return Chainloader.PluginInfos != null && Chainloader.PluginInfos.ContainsKey(guid);
            }
            catch
            {
                // Never let a presence check break the plugin that asked.
                return false;
            }
        }

        /// <summary>The loaded version of a plugin, or null.</summary>
        public static string VersionOf(string guid)
        {
            try
            {
                if (Chainloader.PluginInfos != null
                    && Chainloader.PluginInfos.TryGetValue(guid, out var info))
                    return info?.Metadata?.Version?.ToString();
            }
            catch
            {
                // ignored
            }

            return null;
        }

        /// <summary>A short summary for logging.</summary>
        public static string Summary()
        {
            var more = VersionOf(MoreStraftsGuid);
            var addon = VersionOf(UiSpawnAddonGuid);
            return $"moreStrafts={(more ?? "absent")}, UISpawnAddon={(addon ?? "absent")}";
        }
    }
}
