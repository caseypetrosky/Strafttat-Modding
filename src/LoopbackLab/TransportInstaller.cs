using System;
using FishNet.Managing;
using FishNet.Managing.Transporting;
using FishNet.Transporting.Tugboat;
using HarmonyLib;

namespace LoopbackLab
{
    /// <summary>
    /// Swaps the game's Steam transport for FishNet's Tugboat before FishNet
    /// finishes wiring itself up.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Timing is the whole trick here, and it is not obvious. In FishNet 3.10.8
    /// <c>TransportManager.InitializeOnce_Internal</c> does two things that bind
    /// permanently to whatever <c>Transport</c> happens to be assigned at that
    /// moment: it calls <c>Transport.Initialize(...)</c>, and it caches per-channel
    /// MTUs read from that transport. Immediately afterwards <c>NetworkManager</c>
    /// initialises <c>ClientManager</c> and then <c>ServerManager</c>, and each of
    /// those subscribes its handlers to <i>that transport instance's</i> events
    /// (<c>OnClientReceivedData</c>, <c>OnServerConnectionState</c>, and friends).
    /// </para>
    /// <para>
    /// Assigning a different transport later therefore fails in a particularly
    /// confusing way: connections appear to start, but the managers are still
    /// listening to the old transport, so no data is ever processed. Patching this
    /// method as a <b>prefix</b> means the swap happens before any of that binding,
    /// and every downstream subscription lands on Tugboat naturally — no
    /// re-subscription trickery, no reflection into FishNet internals.
    /// </para>
    /// <para>
    /// A useful side effect: STRAFTAT itself checks
    /// <c>Transport != FishySteamworks</c> and sets its internal
    /// <c>nonSteamworksTransport</c> flag, which puts movement, pause and spawn
    /// handling onto the developers' own non-Steam code path.
    /// </para>
    /// </remarks>
    [HarmonyPatch(typeof(TransportManager), "InitializeOnce_Internal")]
    internal static class TransportInstaller
    {
        /// <summary>Set once the swap has actually happened, for status reporting.</summary>
        public static bool Installed { get; private set; }

        private static void Prefix(TransportManager __instance, NetworkManager manager)
        {
            var config = Plugin.Settings;
            if (config == null || !config.Enabled.Value) return;

            try
            {
                // Live on the same GameObject FishNet expects transports on:
                // TransportManager itself looks for one via gameObject.GetComponent.
                var tugboat = __instance.gameObject.GetComponent<Tugboat>()
                              ?? __instance.gameObject.AddComponent<Tugboat>();

                // Must be set before Initialize: StartConnection reads these
                // fields directly when opening the socket.
                tugboat.SetPort((ushort)config.Port.Value);
                tugboat.SetMaximumClients(config.MaximumClients.Value);
                tugboat.SetClientAddress(config.ClientAddress.Value);

                var previous = __instance.Transport;
                __instance.Transport = tugboat;

                Installed = true;
                Log.Info($"transport swapped: {Describe(previous)} -> Tugboat "
                         + $"(port {config.Port.Value}, max clients {config.MaximumClients.Value})");
                Log.Info($"{manager.name}: loopback mode active — Steam matchmaking is bypassed.");
            }
            catch (Exception e)
            {
                // Leaving Transport untouched means the game boots normally on
                // Steam. A broken test harness must never cost you the game.
                Log.Error($"transport swap failed, staying on the stock transport: {e}");
            }
        }

        private static string Describe(object transport) =>
            transport == null ? "<none>" : transport.GetType().Name;
    }
}
