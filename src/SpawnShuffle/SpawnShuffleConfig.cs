using BepInEx.Configuration;
using UnityEngine;

namespace SpawnShuffle
{
    /// <summary>How to separate players who share a spawn point.</summary>
    internal enum OffsetBehaviour
    {
        /// <summary>Defer to UISpawnAddon when it is present; otherwise do it ourselves.</summary>
        Auto,
        /// <summary>Always apply our own offset, whatever else is installed.</summary>
        Always,
        /// <summary>Never apply our own offset.</summary>
        Never
    }

    /// <summary>All tunables, bound once at startup.</summary>
    internal sealed class SpawnShuffleConfig
    {
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<int> MinimumPlayers;
        public readonly ConfigEntry<bool> OverrideTeamModes;
        public readonly ConfigEntry<float> ClusterRadius;
        public readonly ConfigEntry<OffsetBehaviour> OffsetMode;
        public readonly ConfigEntry<int> Salt;
        public readonly ConfigEntry<KeyboardShortcut> ReportKey;

        public SpawnShuffleConfig(ConfigFile config)
        {
            Enabled = config.Bind(
                "Shuffle", "Enabled", true,
                "Re-deal spawn points every round so you don't start next to the same "
                + "players all match. Turn off to restore vanilla spawn order.");

            MinimumPlayers = config.Bind(
                "Shuffle", "MinimumPlayers", 3,
                new ConfigDescription(
                    "Leave matches smaller than this alone. Vanilla 1v1 spawn handling is "
                    + "already correct (the two players are simply placed apart), so the "
                    + "default skips it.",
                    new AcceptableValueRange<int>(2, 10)));

            OverrideTeamModes = config.Bind(
                "Shuffle", "OverrideTeamModes", false,
                "Also re-deal in team modes. Off by default: the game has hand-tuned 2v2 "
                + "spawn placement that keeps teammates together, and shuffling would "
                + "break that on purpose-built maps.");

            OffsetMode = config.Bind(
                "Shuffle", "SharedPointOffsets", OffsetBehaviour.Auto,
                "How players sharing one spawn point get separated.\n"
                + "Auto: leave it to MoreStrafts_UISpawnAddon when that is installed, since it "
                + "already offsets every spawn, and do it ourselves otherwise. This is the one "
                + "you want.\n"
                + "Always: always apply our own offset. Stacks with the addon's, so players end "
                + "up further apart than ClusterRadius suggests.\n"
                + "Never: never offset. Players sharing a point spawn on top of each other "
                + "unless something else separates them.");

            ClusterRadius = config.Bind(
                "Shuffle", "ClusterRadius", 0.6f,
                new ConfigDescription(
                    "Metres apart when several players share one spawn point, which happens "
                    + "above 4 players because maps only ship 1v1 and 4-player spawn sets. "
                    + "Too small and players spawn inside each other; too large and they "
                    + "land in walls.",
                    new AcceptableValueRange<float>(0f, 3f)));

            ReportKey = config.Bind(
                "Hotkeys", "Report", new KeyboardShortcut(KeyCode.F10),
                "Log what this plugin is doing, including the deal it would make right now. "
                + "Works in a one-player lobby, where the shuffle itself does not run.");

            Salt = config.Bind(
                "Shuffle", "Salt", 0,
                "Change this for a different sequence of deals. Same salt plus same round "
                + "always produces the same arrangement, which makes bugs reproducible.");
        }
    }
}
