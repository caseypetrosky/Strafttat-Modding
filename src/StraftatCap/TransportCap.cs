// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using FishNet;
using FishNet.Transporting;

namespace StraftatCap
{
    /// <summary>
    /// Keeps the transport's client allowance in step with the lobby's cap.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this is not simply "call SetMaximumClients when the cap changes".</b>
    /// FishySteamworks stores its own serialised <c>_maximumClients</c> field,
    /// and STRAFTAT ships it set to 3 - a four-player lobby, host included.
    /// <c>SetMaximumClients</c> is a proper override, but it writes only to the
    /// live server socket:
    /// </para>
    /// <code>
    /// public override void SetMaximumClients(int value) => _server.SetMaximumClients(value);
    /// </code>
    /// <para>
    /// Starting the server then does this:
    /// </para>
    /// <code>
    /// _server.StartConnection(_serverBindAddress, _port, _maximumClients, _peerToPeer);
    /// // ServerSocket.StartConnection -> SetMaximumClients(maximumClients)
    /// </code>
    /// <para>
    /// It passes the <i>serialised</i> field, and the socket overwrites whatever
    /// it was told earlier. So a cap applied before the server starts is thrown
    /// away at start, silently: the Steam lobby still advertises ten slots while
    /// the transport accepts three clients.
    /// </para>
    /// <para>
    /// The cap therefore has to be re-applied <b>after</b> the server reports
    /// Started, which is what this class exists to do. It listens to FishNet's
    /// own connection-state event rather than patching FishySteamworks.
    /// </para>
    /// </remarks>
    internal static class TransportCap
    {
        private static bool _subscribed;

        /// <summary>Last value we successfully pushed, for reporting.</summary>
        public static int LastApplied { get; private set; } = -1;

        /// <summary>
        /// Subscribes to the server's connection state. Safe to call repeatedly;
        /// it only takes effect once FishNet exists and only subscribes once.
        /// </summary>
        public static void EnsureSubscribed()
        {
            if (_subscribed) return;

            var serverManager = InstanceFinder.ServerManager;
            if (serverManager == null) return;   // no NetworkManager yet

            serverManager.OnServerConnectionState += OnServerConnectionState;
            _subscribed = true;
            Log.Info("listening for server start, to re-apply the cap afterwards.");
        }

        private static void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            // Only once the socket is actually up. Applying while it is starting
            // would land before StartConnection overwrites the value, which is
            // the exact trap this class exists to avoid.
            if (args.ConnectionState != LocalConnectionState.Started) return;

            Apply("server started");
        }

        /// <summary>
        /// Pushes the current lobby cap to the transport.
        /// </summary>
        public static void Apply(string reason)
        {
            try
            {
                if (!GameBridge.Usable) return;

                var transport = InstanceFinder.TransportManager?.Transport;
                if (transport == null) return;

                int maxPlayers = GameBridge.MaxPlayers();
                if (maxPlayers <= 0) return;

                int clients = CapMath.TransportClientsFor(maxPlayers);
                transport.SetMaximumClients(clients);
                LastApplied = clients;

                Log.Info($"transport cap -> {clients} clients ({maxPlayers} players) [{reason}]");
            }
            catch (Exception e)
            {
                Log.Error($"applying the transport cap failed ({reason}): {e}");
            }
        }

        /// <summary>What the transport currently reports, or -1 if unavailable.</summary>
        public static int CurrentTransportClients()
        {
            try
            {
                var transport = InstanceFinder.TransportManager?.Transport;
                return transport == null ? -1 : transport.GetMaximumClients();
            }
            catch
            {
                return -1;
            }
        }
    }
}
