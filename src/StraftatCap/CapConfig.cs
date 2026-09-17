// SPDX-License-Identifier: GPL-3.0-or-later
using BepInEx.Configuration;
using UnityEngine;

namespace StraftatCap
{
    /// <summary>All tunables, bound once at startup.</summary>
    internal sealed class CapConfig
    {
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<int> MaxPlayers;
        public readonly ConfigEntry<bool> DeferToMoreStrafts;
        public readonly ConfigEntry<KeyboardShortcut> ReportKey;

        public CapConfig(ConfigFile config)
        {
            Enabled = config.Bind(
                "Cap", "Enabled", true,
                "Raise the lobby player cap. Off restores vanilla's limit of four.");

            MaxPlayers = config.Bind(
                "Cap", "MaxPlayers", CapMath.MaxPlayers,
                new ConfigDescription(
                    "Largest lobby the dropdown will offer. Everyone in a lobby should run "
                    + "the same value: vanilla makes each client kick itself if its own copy "
                    + "of the cap is smaller than its position in the player list.",
                    new AcceptableValueRange<int>(CapMath.MinPlayers, CapMath.MaxPlayers)));

            DeferToMoreStrafts = config.Bind(
                "Cap", "DeferToMoreStrafts", true,
                "Stand down when moreStrafts is installed, since it raises the cap too and two "
                + "mods writing the same values would fight. Set false only if you want this "
                + "mod to take over instead - disable moreStrafts' own capacity handling first.");

            ReportKey = config.Bind(
                "Hotkeys", "Report", new KeyboardShortcut(KeyCode.F9),
                "Log every cap-related value at once, so a lobby can be checked without "
                + "a second player.");
        }
    }
}
