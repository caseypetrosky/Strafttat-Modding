using System;
using FishNet;

namespace LoopbackLab
{
    /// <summary>
    /// Starting and stopping loopback connections.
    /// </summary>
    /// <remarks>
    /// These go through FishNet's <c>ServerManager</c> / <c>ClientManager</c>
    /// rather than poking the transport directly. STRAFTAT calls
    /// <c>StartConnection</c> straight on its FishySteamworks instance, but the
    /// manager-level calls are the supported entry points: they run FishNet's own
    /// state checks and raise the connection-state events the rest of the stack
    /// (and the game) listens for.
    /// </remarks>
    internal static class LoopbackSession
    {
        /// <summary>
        /// Starts a server and connects this instance's own client to it —
        /// the equivalent of STRAFTAT hosting a Steam lobby.
        /// </summary>
        public static void Host()
        {
            if (!Guard(out var reason)) { Log.Warn($"host: {reason}"); return; }

            if (InstanceFinder.IsServer)
            {
                Log.Warn("host: already hosting; ignoring.");
                return;
            }

            if (!InstanceFinder.ServerManager.StartConnection())
            {
                Log.Error("host: server failed to start (port already in use?).");
                return;
            }

            // Connect our own client so the host is a player too, not a
            // headless server. Address is irrelevant for a local connection
            // but FishNet still wants one it can dial.
            if (!InstanceFinder.ClientManager.StartConnection())
            {
                Log.Error("host: server started but the local client failed to connect.");
                return;
            }

            Log.Info("host: server + local client started.");
            SceneMotorSpawner.SpawnIfNeeded();
        }

        /// <summary>
        /// Connects this instance to a loopback host — the equivalent of
        /// joining someone's Steam lobby.
        /// </summary>
        public static void Join()
        {
            if (!Guard(out var reason)) { Log.Warn($"join: {reason}"); return; }

            if (InstanceFinder.IsClient)
            {
                Log.Warn("join: already connected; press the Stop hotkey first.");
                return;
            }

            var address = Plugin.Settings.ClientAddress.Value;
            if (!InstanceFinder.ClientManager.StartConnection(address))
            {
                Log.Error($"join: could not start a connection to {address}.");
                return;
            }

            Log.Info($"join: connecting to {address}...");
        }

        /// <summary>Disconnects the client and, when hosting, stops the server.</summary>
        public static void Stop()
        {
            if (!Guard(out var reason)) { Log.Warn($"stop: {reason}"); return; }

            if (InstanceFinder.IsClient) InstanceFinder.ClientManager.StopConnection();

            // sendDisconnectMessage: tell connected clients why they're being
            // dropped instead of letting them time out.
            if (InstanceFinder.IsServer) InstanceFinder.ServerManager.StopConnection(true);

            Log.Info("stop: connections closed.");
        }

        /// <summary>
        /// Verifies loopback mode is actually live. Without this, the hotkeys
        /// would happily start a Tugboat-less connection on the Steam transport
        /// and produce baffling failures.
        /// </summary>
        private static bool Guard(out string reason)
        {
            if (Plugin.Settings == null || !Plugin.Settings.Enabled.Value)
            {
                reason = "loopback is disabled in the config (set Enabled = true and restart).";
                return false;
            }

            if (!TransportInstaller.Installed)
            {
                reason = "the Tugboat transport was never installed — check the log for a swap failure.";
                return false;
            }

            if (InstanceFinder.NetworkManager == null)
            {
                reason = "no NetworkManager in the scene yet; get to the main menu first.";
                return false;
            }

            reason = null;
            return true;
        }
    }
}
