using BepInEx.Configuration;

namespace SpawnShuffle
{
    /// <summary>All tunables, bound once at startup.</summary>
    internal sealed class SpawnShuffleConfig
    {
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<int> MinimumPlayers;
        public readonly ConfigEntry<bool> OverrideTeamModes;
        public readonly ConfigEntry<float> ClusterRadius;
        public readonly ConfigEntry<int> Salt;

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

            ClusterRadius = config.Bind(
                "Shuffle", "ClusterRadius", 0.6f,
                new ConfigDescription(
                    "Metres apart when several players share one spawn point, which happens "
                    + "above 4 players because maps only ship 1v1 and 4-player spawn sets. "
                    + "Too small and players spawn inside each other; too large and they "
                    + "land in walls.",
                    new AcceptableValueRange<float>(0f, 3f)));

            Salt = config.Bind(
                "Shuffle", "Salt", 0,
                "Change this for a different sequence of deals. Same salt plus same round "
                + "always produces the same arrangement, which makes bugs reproducible.");
        }
    }
}
