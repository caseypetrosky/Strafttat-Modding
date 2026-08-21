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
    [BepInPlugin(Guid, Name, Version)]
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

        private void Awake()
        {
            Log.Init(Logger);
            Settings = new SpawnShuffleConfig(Config);

            // Patch unconditionally; the prefix decides what to do. That keeps
            // toggling the config independent of whether patching succeeded.
            new Harmony(Guid).PatchAll(typeof(RoundSpawnPatch));

            Log.Info(Settings.Enabled.Value
                ? $"{Name} v{Version} loaded — spawns re-dealt each round "
                  + $"(min {Settings.MinimumPlayers.Value} players, salt {Settings.Salt.Value})."
                : $"{Name} v{Version} loaded — disabled in config, spawns stay vanilla.");
        }
    }
}
