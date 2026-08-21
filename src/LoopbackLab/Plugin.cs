using System;
using BepInEx;
using HarmonyLib;

namespace LoopbackLab
{
    /// <summary>
    /// Test harness for running several STRAFTAT instances on one machine.
    /// </summary>
    /// <remarks>
    /// The shipping netcode is FishySteamworks, where a "network address" is a
    /// Steam ID. Two instances signed into the same account share one Steam ID
    /// and so cannot address each other. This plugin sidesteps that by putting
    /// FishNet on its Tugboat transport (plain TCP/UDP over 127.0.0.1), which is
    /// already compiled into the game.
    ///
    /// Strictly a development tool: with Enabled = false (the default) it patches
    /// the transport selection but changes nothing, so it is safe to leave
    /// installed while playing online.
    /// </remarks>
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.caseypetrosky.loopbacklab";
        public const string Name = "Loopback Lab";
        public const string Version = "0.1.0";

        /// <summary>
        /// Our own settings. Deliberately not called "Config": BaseUnityPlugin
        /// already has a <c>Config</c> property (the raw BepInEx config file),
        /// and having both under one name reads badly at every call site.
        /// </summary>
        internal static LoopbackConfig Settings { get; private set; }

        private void Awake()
        {
            Log.Init(Logger);
            Settings = new LoopbackConfig(Config);

            // Patch unconditionally: the prefix itself checks Enabled. Doing it
            // this way means toggling the config never depends on whether the
            // patch was applied, only on what it decides to do.
            new Harmony(Guid).PatchAll(typeof(TransportInstaller));

            if (Settings.Enabled.Value)
            {
                Log.Info($"{Name} v{Version}: loopback ENABLED, waiting for FishNet to initialise.");
                Log.Info($"{Settings.HostKey.Value} = host | {Settings.JoinKey.Value} = join | "
                         + $"{Settings.StopKey.Value} = stop");
            }
            else
            {
                Log.Info($"{Name} v{Version}: loopback disabled — normal Steam play. "
                         + "Set Loopback.Enabled = true and restart to use it.");
            }
        }

        private void Update()
        {
            // Cheap early-out so a disabled plugin costs nothing per frame, and
            // so the hotkeys can't fire during normal online play.
            if (Settings == null || !Settings.Enabled.Value) return;

            try
            {
                if (Settings.HostKey.Value.IsDown()) LoopbackSession.Host();
                else if (Settings.JoinKey.Value.IsDown()) LoopbackSession.Join();
                else if (Settings.StopKey.Value.IsDown()) LoopbackSession.Stop();
            }
            catch (Exception e)
            {
                // A test harness must never be the reason the game dies.
                Log.Error($"hotkey handler failed: {e}");
            }
        }
    }
}
