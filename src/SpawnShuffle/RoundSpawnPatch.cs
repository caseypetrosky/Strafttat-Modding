using System;
using HarmonyLib;
using UnityEngine;

namespace SpawnShuffle
{
    /// <summary>
    /// Replaces the game's choice of spawn point at the start of each round.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Which method, and why it matters.</b> <c>PlayerManager</c> has two
    /// <c>SpawnPlayer</c> overloads. The four-argument one
    /// (<c>suit, cig, position, rotation</c>) is the dumb "put a body here" call,
    /// and it is also used by <c>CmdRespawn</c> for mid-round respawns, which
    /// pick a free point via <c>ReturnSpawnPoint</c>. Patching that would hijack
    /// respawns too. The two-argument one is where the round-start
    /// <i>decision</i> is made, so that is what we take over — respawn behaviour
    /// is left completely alone.
    /// </para>
    /// <para>
    /// The prefix returns false (skipping vanilla) only when it has actually
    /// produced a position. Every bail-out returns true, so any failure falls
    /// through to the original method rather than leaving a player unspawned.
    /// </para>
    /// <para>
    /// This runs inside a <c>[ServerRpc]</c>, so it executes on the server, once
    /// per player, in separate invocations — which is exactly why
    /// <see cref="SpawnAssignment"/> is a pure function of the round rather than
    /// anything stateful.
    /// </para>
    /// </remarks>
    [HarmonyPatch]
    internal static class RoundSpawnPatch
    {
        private static bool _loggedFailure;

        private static System.Reflection.MethodBase TargetMethod() =>
            AccessTools.Method(AccessTools.TypeByName("PlayerManager"), "SpawnPlayer",
                new[] { typeof(int), typeof(int) });

        /// <summary>How many spawns this plugin has actually placed.</summary>
        internal static int AppliedCount { get; private set; }

        /// <summary>
        /// Why the last spawn was or was not re-dealt.
        /// </summary>
        /// <remarks>
        /// Every bail-out below hands back to vanilla, which is correct but
        /// indistinguishable from the plugin not running at all - the failure
        /// mode that hid a broken singleton lookup for an entire release. The
        /// reason is recorded so the status report can say which it was.
        /// </remarks>
        internal static string LastOutcome { get; private set; } = "no spawn seen yet";

        private static bool Skip(string reason)
        {
            LastOutcome = reason;
            return true;   // let vanilla place the player
        }

        private static bool Prefix(object __instance, int suitIndex, int cigIndex)
        {
            var settings = Plugin.Settings;
            if (settings == null || !settings.Enabled.Value) return Skip("disabled in config");

            try
            {
                if (!GameAccess.Usable) return Skip($"game members unresolved ({GameAccess.MissingMembers()})");

                var players = GameAccess.ConnectedPlayerIds();
                if (players.Count < settings.MinimumPlayers.Value)
                    return Skip($"{players.Count} player(s), minimum is {settings.MinimumPlayers.Value}");

                if (!settings.OverrideTeamModes.Value && TeamModeActive())
                    return Skip("team mode, and OverrideTeamModes is off");

                int playerId = GameAccess.PlayerIdOf(__instance);
                if (playerId < 0) return Skip("player id unknown");

                // Vanilla refreshes the spawn list here; after a map change the
                // cached array is otherwise stale.
                GameAccess.RefreshSpawnPoints(__instance);

                var points = GameAccess.SpawnPoints(__instance);
                if (points.Count == 0) return Skip("the map reports no spawn points");

                int round = GameAccess.RoundIndex();
                var slot = SpawnAssignment.Assign(playerId, players, points.Count, round, settings.Salt.Value);

                var point = points[slot.SpawnPointIndex];
                if (point == null) return Skip($"spawn point {slot.SpawnPointIndex} is null");

                var position = point.position;
                if (SpawnAssignment.NeedsOffset(slot) && Plugin.ShouldOffsetSharedPoints)
                {
                    double angle = SpawnAssignment.AngleFor(slot, round, settings.Salt.Value);
                    float radius = settings.ClusterRadius.Value;
                    // Offset in the horizontal plane only: nudging Y would drop
                    // players through the floor or float them above it.
                    position += new Vector3(
                        (float)Math.Cos(angle) * radius, 0f, (float)Math.Sin(angle) * radius);
                }

                var rotation = Quaternion.Euler(0f, point.eulerAngles.y, 0f);
                GameAccess.SpawnAt(__instance, suitIndex, cigIndex, position, rotation);

                AppliedCount++;
                LastOutcome = $"round {round}: player {playerId} -> point {slot.SpawnPointIndex}"
                              + (slot.Occupants > 1 ? $" (sharing with {slot.Occupants - 1}, ring {slot.Ring})" : " (alone)");
                Log.Info(LastOutcome);

                return false;
            }
            catch (Exception e)
            {
                // Log once, then stay quiet: this runs every spawn and a
                // repeating stack trace would bury everything else.
                if (!_loggedFailure)
                {
                    _loggedFailure = true;
                    Log.Error($"spawn assignment failed, falling back to vanilla: {e}");
                }
                return Skip($"threw: {e.GetType().Name}");
            }
        }

        /// <summary>
        /// Whether the match is in a team mode. Read reflectively from
        /// GameManager, and treated as "yes" if it cannot be determined, so an
        /// unknown state errs towards leaving the game's own placement alone.
        /// </summary>
        private static bool TeamModeActive()
        {
            try
            {
                var gameManagerType = AccessTools.TypeByName("GameManager");
                // Field or property; GameManager.Instance is a field here.
                var instance = StraftatModding.StaticAccess.Read(gameManagerType, "Instance");
                if (instance == null) return false;

                var playingTeams = AccessTools.Field(gameManagerType, "playingTeams");
                return playingTeams != null && (bool)playingTeams.GetValue(instance);
            }
            catch
            {
                return true;
            }
        }
    }
}
