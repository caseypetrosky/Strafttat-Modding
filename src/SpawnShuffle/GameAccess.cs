using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SpawnShuffle
{
    /// <summary>
    /// Every reach into the game's own types lives here, so the rest of the
    /// plugin talks in plain values and the blast radius of a game update is
    /// one file.
    /// </summary>
    /// <remarks>
    /// Reflection rather than a compile-time <c>Assembly-CSharp</c> reference:
    /// the fields we need are private, and a rename should degrade to a logged
    /// warning and vanilla behaviour rather than a plugin that fails to load.
    /// Members are resolved once and cached — <c>AccessTools</c> lookups are not
    /// cheap enough to run per spawn.
    /// </remarks>
    internal static class GameAccess
    {
        private static bool _resolved;
        private static bool _usable;

        private static FieldInfo _currentSpawnPoints;   // PlayerManager.CurrentSpawnPoints
        private static FieldInfo _clientScript;         // PlayerManager.ClientScript
        private static FieldInfo _playerId;             // ClientInstance.PlayerId
        private static FieldInfo _playerInstances;      // ClientInstance.playerInstances (static)
        private static PropertyInfo _scoreInstance;     // ScoreManager.Instance
        private static FieldInfo _takeIndex;            // ScoreManager.TakeIndex
        private static MethodInfo _setActiveSpawnPoints;// PlayerManager.SetActiveSpawnPoints
        private static MethodInfo _spawnAt;             // PlayerManager.SpawnPlayer(int,int,Vector3,Quaternion)

        /// <summary>
        /// True when every member we need was found. Checked once; if anything
        /// is missing the plugin stands down and the game behaves normally.
        /// </summary>
        public static bool Usable
        {
            get
            {
                if (!_resolved) Resolve();
                return _usable;
            }
        }

        private static void Resolve()
        {
            _resolved = true;
            try
            {
                var playerManager = AccessTools.TypeByName("PlayerManager");
                var clientInstance = AccessTools.TypeByName("ClientInstance");
                var scoreManager = AccessTools.TypeByName("ScoreManager");
                if (playerManager == null || clientInstance == null || scoreManager == null)
                {
                    Log.Warn("could not find PlayerManager/ClientInstance/ScoreManager; standing down.");
                    return;
                }

                _currentSpawnPoints = AccessTools.Field(playerManager, "CurrentSpawnPoints");
                _clientScript = AccessTools.Field(playerManager, "ClientScript");
                _playerId = AccessTools.Field(clientInstance, "PlayerId");
                _playerInstances = AccessTools.Field(clientInstance, "playerInstances");
                _scoreInstance = AccessTools.Property(scoreManager, "Instance");
                _takeIndex = AccessTools.Field(scoreManager, "TakeIndex");
                _setActiveSpawnPoints = AccessTools.Method(playerManager, "SetActiveSpawnPoints");
                _spawnAt = AccessTools.Method(playerManager, "SpawnPlayer",
                    new[] { typeof(int), typeof(int), typeof(Vector3), typeof(Quaternion) });

                _usable = _currentSpawnPoints != null && _clientScript != null && _playerId != null
                          && _playerInstances != null && _scoreInstance != null && _takeIndex != null
                          && _setActiveSpawnPoints != null && _spawnAt != null;

                if (!_usable) Log.Warn($"game layout changed ({Missing()}); standing down, spawns stay vanilla.");
                else Log.Info("game members resolved.");
            }
            catch (Exception e)
            {
                Log.Error($"resolving game members failed: {e}");
                _usable = false;
            }
        }

        private static string Missing()
        {
            var missing = new List<string>();
            if (_currentSpawnPoints == null) missing.Add("PlayerManager.CurrentSpawnPoints");
            if (_clientScript == null) missing.Add("PlayerManager.ClientScript");
            if (_playerId == null) missing.Add("ClientInstance.PlayerId");
            if (_playerInstances == null) missing.Add("ClientInstance.playerInstances");
            if (_scoreInstance == null) missing.Add("ScoreManager.Instance");
            if (_takeIndex == null) missing.Add("ScoreManager.TakeIndex");
            if (_setActiveSpawnPoints == null) missing.Add("PlayerManager.SetActiveSpawnPoints");
            if (_spawnAt == null) missing.Add("PlayerManager.SpawnPlayer(int,int,Vector3,Quaternion)");
            return string.Join(", ", missing);
        }

        /// <summary>
        /// Refreshes the map's spawn point list, exactly as vanilla does at the
        /// top of its own round-spawn method. Skipping this leaves a stale list
        /// after a map change.
        /// </summary>
        public static void RefreshSpawnPoints(object playerManager) =>
            _setActiveSpawnPoints.Invoke(playerManager, null);

        /// <summary>The spawn points the current map is using, as transforms.</summary>
        public static IReadOnlyList<Transform> SpawnPoints(object playerManager)
        {
            var points = _currentSpawnPoints.GetValue(playerManager) as Array;
            if (points == null) return Array.Empty<Transform>();

            var transforms = new List<Transform>(points.Length);
            foreach (var point in points)
                transforms.Add((point as Component)?.transform);
            return transforms;
        }

        /// <summary>The player id this PlayerManager belongs to, or -1.</summary>
        public static int PlayerIdOf(object playerManager)
        {
            var client = _clientScript.GetValue(playerManager);
            return client == null ? -1 : (int)_playerId.GetValue(client);
        }

        /// <summary>Ids of everyone currently in the match.</summary>
        public static List<int> ConnectedPlayerIds()
        {
            var ids = new List<int>();
            if (_playerInstances.GetValue(null) is IDictionary instances)
                foreach (var key in instances.Keys)
                    ids.Add((int)key);
            return ids;
        }

        /// <summary>The round counter. Synced to clients by the game as a SyncVar.</summary>
        public static int RoundIndex()
        {
            var score = _scoreInstance.GetValue(null);
            return score == null ? 0 : (int)_takeIndex.GetValue(score);
        }

        /// <summary>Hands the chosen position back to the game's own spawn routine.</summary>
        public static void SpawnAt(object playerManager, int suitIndex, int cigIndex,
                                   Vector3 position, Quaternion rotation) =>
            _spawnAt.Invoke(playerManager, new object[] { suitIndex, cigIndex, position, rotation });
    }
}
