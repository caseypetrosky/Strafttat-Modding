using BepInEx;
using UnityEngine;
using System.Collections.Generic;
using System;
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
        public const string Guid = "spawnshuffle";
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

        private int _lastTickFrame = -1;

        private void Awake()
        {
            Log.Init(Logger);
            Settings = new SpawnShuffleConfig(Config);

            ShouldOffsetSharedPoints = ResolveOffsetBehaviour();

            // Patch unconditionally; the prefix decides what to do. That keeps
            // toggling the config independent of whether patching succeeded.
            new Harmony(Guid).PatchAll(typeof(RoundSpawnPatch));

            // Update alone is not dependable here - the game destroys plugin
            // components, so Unity stops calling it. onBeforeRender is a static
            // player-loop event and keeps running regardless.
            Application.onBeforeRender += OnBeforeRender;

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


        private void Update() => Tick();
        private void OnBeforeRender() => Tick();

        private void Tick()
        {
            try
            {
                if (_lastTickFrame == Time.frameCount) return;
                _lastTickFrame = Time.frameCount;

                if (Settings == null) return;
                if (Settings.ReportKey.Value.IsDown() || Input.GetKeyDown(KeyCode.F10)) Report();
            }
            catch (Exception e)
            {
                Log.Error($"tick failed: {e}");
            }
        }

        /// <summary>
        /// Logs what this plugin is doing and what it would do right now.
        /// </summary>
        /// <remarks>
        /// Deliberately useful in a one-player lobby, where the shuffle itself
        /// does not run: the dry run below deals a hypothetical roster against
        /// the real spawn points and the real round number, so the wiring can be
        /// checked without gathering a group.
        /// </remarks>
        private static void Report()
        {
            try
            {
                Log.Info("---- spawn shuffle report ----");
                Log.Info($"enabled                 : {Settings.Enabled.Value}");
                Log.Info($"detected mods           : {StraftatModding.ModPresence.Summary()}");
                Log.Info($"we offset shared points : {ShouldOffsetSharedPoints}"
                         + (ShouldOffsetSharedPoints ? "" : " (left to UISpawnAddon)"));
                Log.Info($"spawns re-dealt so far  : {RoundSpawnPatch.AppliedCount}");
                Log.Info($"last spawn outcome      : {RoundSpawnPatch.LastOutcome}");

                if (!GameAccess.Usable)
                {
                    Log.Warn($"NOT running. Could not find: {GameAccess.MissingMembers()}");
                    Log.Info("------------------------------");
                    return;
                }

                var players = GameAccess.ConnectedPlayerIds();
                int round = GameAccess.RoundIndex();
                Log.Info($"round index             : {round}");
                Log.Info($"players present         : {players.Count} [{string.Join(",", players)}]");
                Log.Info($"minimum to act          : {Settings.MinimumPlayers.Value}");

                // Spawn points come from a live PlayerManager; without one there
                // is nothing to deal against and a dry run would be fiction.
                var manager = GameAccess.AnyPlayerManager();
                if (manager == null)
                {
                    Log.Info("spawn points            : no PlayerManager yet (start a match)");
                    Log.Info("------------------------------");
                    return;
                }

                GameAccess.RefreshSpawnPoints(manager);
                var points = GameAccess.SpawnPoints(manager);
                Log.Info($"spawn points on this map: {points.Count}");

                if (points.Count == 0)
                {
                    Log.Info("------------------------------");
                    return;
                }

                // Dry run. Uses the real round and real point count, so the deal
                // shown is the one that would happen with this many players.
                int sample = Math.Max(players.Count, Settings.MinimumPlayers.Value);
                var roster = new List<int>();
                for (int i = 0; i < sample; i++) roster.Add(i);

                Log.Info($"dry run for {sample} players (round {round}):");
                for (int i = 0; i < sample; i++)
                {
                    var slot = SpawnAssignment.Assign(i, roster, points.Count, round, Settings.Salt.Value);
                    Log.Info($"   player {i} -> point {slot.SpawnPointIndex}"
                             + (slot.Occupants > 1
                                 ? $" (shared by {slot.Occupants}, ring {slot.Ring})"
                                 : " (alone)"));
                }

                Log.Info("------------------------------");
            }
            catch (Exception e)
            {
                Log.Error($"report failed: {e}");
            }
        }

    }
}
