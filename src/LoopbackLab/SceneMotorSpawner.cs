using System;
using FishNet;
using HarmonyLib;
using UnityEngine;

namespace LoopbackLab
{
    /// <summary>
    /// Reproduces the one piece of hosting the game does for itself that we
    /// skip by bypassing Steam matchmaking: spawning the networked SceneMotor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When STRAFTAT hosts normally it instantiates a <c>SceneMotor</c> prefab and
    /// hands it to <c>ServerManager.Spawn</c>. Nothing else spawns it, and other
    /// systems read <c>SceneMotor.Instance</c>, so a loopback host that skips this
    /// step ends up in a half-working match.
    /// </para>
    /// <para>
    /// Everything here is reflective, on purpose. The prefab is a private
    /// <c>[SerializeField]</c> on <c>SteamLobby</c>, and reaching it through
    /// Harmony's <c>AccessTools</c> — rather than referencing the game assembly —
    /// keeps this plugin's compile-time dependencies to FishNet, BepInEx and Unity.
    /// That means it still loads (and logs a clear message) if a game update
    /// renames things, instead of failing to resolve a type at load time.
    /// </para>
    /// </remarks>
    internal static class SceneMotorSpawner
    {
        private const string SteamLobbyType = "SteamLobby";
        private const string PrefabField = "_sceneMotorPrefab";
        private const string SceneMotorType = "SceneMotor";

        /// <summary>
        /// Spawns the SceneMotor if the game hasn't already got one. Failure is
        /// logged, never thrown: a connected-but-motorless session is still
        /// useful for testing lobby UI.
        /// </summary>
        public static void SpawnIfNeeded()
        {
            try
            {
                if (!InstanceFinder.IsServer)
                {
                    Log.Warn("scene motor: not the server, skipping.");
                    return;
                }

                if (AlreadyPresent())
                {
                    Log.Info("scene motor: already present, skipping.");
                    return;
                }

                var lobby = FindSteamLobby();
                if (lobby == null)
                {
                    Log.Warn($"scene motor: no live {SteamLobbyType} found; skipping spawn.");
                    return;
                }

                var prefab = AccessTools.Field(lobby.GetType(), PrefabField)?.GetValue(lobby) as GameObject;
                if (prefab == null)
                {
                    Log.Warn($"scene motor: {SteamLobbyType}.{PrefabField} missing or null "
                             + "(game update?); skipping spawn.");
                    return;
                }

                var motor = UnityEngine.Object.Instantiate(
                    prefab, lobby.transform.position, Quaternion.identity);
                InstanceFinder.ServerManager.Spawn(motor);
                Log.Info("scene motor: spawned.");
            }
            catch (Exception e)
            {
                Log.Error($"scene motor: spawn failed: {e}");
            }
        }

        private static bool AlreadyPresent()
        {
            var type = AccessTools.TypeByName(SceneMotorType);
            return type != null && UnityEngine.Object.FindObjectOfType(type) != null;
        }

        private static Component FindSteamLobby()
        {
            var type = AccessTools.TypeByName(SteamLobbyType);
            if (type == null) return null;
            return UnityEngine.Object.FindObjectOfType(type) as Component;
        }
    }
}
