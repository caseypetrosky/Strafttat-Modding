using BepInEx;
using HarmonyLib;

namespace SpawnShuffle
{
    /// <summary>
    /// Re-deals spawn points every round so a match stops replaying the same
    /// opening fight.
    /// </summary>
    /// <remarks>
    /// Vanilla places players at <c>spawnPoints[(TakeIndex + PlayerId) % count]</c>.
    /// Everyone advances one point per round in lockstep, so the arrangement only
    /// rotates and the gap between any two players never changes — you spawn next
    /// to the same people every round for the whole match. Above four players it
    /// also collides, always between the same ids, because maps only ship 1v1 and
    /// 4-player spawn sets.
    ///
    /// Works with or without moreStrafts. moreStrafts itself contains no spawn
    /// code at all; see the README for how this sits alongside the UISpawnAddon,
    /// which does.
    /// </remarks>
    // Soft dependencies: we work perfectly well alone, but when these are
    // installed BepInEx loads us after them, so the presence checks below see
    // them rather than racing the chainloader.
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(StraftatModding.ModPresence.MoreStraftsGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(StraftatModding.ModPresence.UiSpawnAddonGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.caseypetrosky.spawnshuffle";
        public const string Name = "Spawn Shuffle";
        public const string Version = "0.1.0";

        /// <summary>
        /// Our settings. Not named "Config" — BaseUnityPlugin already has a
        /// Config property, and shadowing it reads badly at call sites.
        /// </summary>
        internal static SpawnShuffleConfig Settings { get; private set; }

        /// <summary>
        /// Whether we separate players who share a spawn point, or leave it to
        /// another mod that already does.
        /// </summary>
        /// <remarks>
        /// MoreStrafts_UISpawnAddon prefixes the four-argument SpawnPlayer and
        /// offsets every spawn on its own circle. We patch a different overload,
        /// so both apply and the offsets stack - players end up further apart
        /// than either mod intends. Deciding this once, at load, keeps the two
        /// composing cleanly: we choose who shares a point, it separates them.
        /// </remarks>
        internal static bool ShouldOffsetSharedPoints { get; private set; } = true;

        private void Awake()
        {
            Log.Init(Logger);
            Settings = new SpawnShuffleConfig(Config);

            ShouldOffsetSharedPoints = ResolveOffsetBehaviour();

            // Patch unconditionally; the prefix decides what to do. That keeps
            // toggling the config independent of whether patching succeeded.
            new Harmony(Guid).PatchAll(typeof(RoundSpawnPatch));

            Log.Info($"detected mods: {StraftatModding.ModPresence.Summary()}");
            Log.Info(ShouldOffsetSharedPoints
                ? "separating players who share a spawn point ourselves."
                : "leaving shared-point separation to MoreStrafts_UISpawnAddon.");

            Log.Info(Settings.Enabled.Value
                ? $"{Name} v{Version} loaded — spawns re-dealt each round "
                  + $"(min {Settings.MinimumPlayers.Value} players, salt {Settings.Salt.Value})."
                : $"{Name} v{Version} loaded — disabled in config, spawns stay vanilla.");
        }
        /// <summary>Applies the configured policy against what is actually loaded.</summary>
        private static bool ResolveOffsetBehaviour()
        {
            switch (Settings.OffsetMode.Value)
            {
                case OffsetBehaviour.Always: return true;
                case OffsetBehaviour.Never: return false;
                default: return !StraftatModding.ModPresence.UiSpawnAddon;
            }
        }

    }
}
