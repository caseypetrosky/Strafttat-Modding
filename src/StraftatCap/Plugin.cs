// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using BepInEx;
using FishNet;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StraftatCap
{
    /// <summary>
    /// Raises STRAFTAT's lobby cap above four.
    /// </summary>
    /// <remarks>
    /// The cap itself is soft - vanilla derives it from a dropdown whose three
    /// options are authored in the scene - so widening the dropdown is most of
    /// the work. The part that is not obvious is the transport; see
    /// <see cref="TransportCap"/>.
    ///
    /// This is the capacity layer only. It does not touch the lobby UI, the
    /// round-end screen or the tab screen, all of which still assume four
    /// players in ways that need real UI work rather than a bigger number.
    /// </remarks>
    // Soft dependency: we do not need moreStrafts, but if it is installed we
    // want to load after it so the check below sees it.
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(StraftatModding.ModPresence.MoreStraftsGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.caseypetrosky.straftatcap";
        public const string Name = "Straftat Cap";
        public const string Version = "0.1.0";

        /// <summary>Our settings. Not "Config" - BaseUnityPlugin already has one.</summary>
        internal static CapConfig Settings { get; private set; }

        /// <summary>The configured ceiling, already clamped to what is supported.</summary>
        internal static int MaxPlayers => CapMath.Clamp(Settings?.MaxPlayers?.Value ?? CapMath.MaxPlayers);

        /// <summary>
        /// False when another mod owns the cap, in which case every patch here
        /// becomes a no-op.
        /// </summary>
        /// <remarks>
        /// moreStrafts raises the cap as well. Two mods writing the same
        /// dropdown and the same transport value would race, and whichever wrote
        /// last would win - which is not a thing to leave to chance. Standing
        /// down is the polite default; the F9 report still works, so this is
        /// also a way to inspect a moreStrafts lobby.
        /// </remarks>
        internal static bool Active { get; private set; } = true;

        private int _lastTickFrame = -1;

        private void Awake()
        {
            Log.Init(Logger);
            Settings = new CapConfig(Config);

            bool deferring = Settings.DeferToMoreStrafts.Value && StraftatModding.ModPresence.MoreStrafts;
            Active = Settings.Enabled.Value && !deferring;

            // Patch regardless; each patch checks Active. Keeping patching
            // unconditional means the config decides behaviour, not whether the
            // patch happened to be applied.
            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);

            Log.Info($"detected mods: {StraftatModding.ModPresence.Summary()}");
            if (deferring)
                Log.Info("moreStrafts is installed and owns the cap; standing down. "
                         + "F9 still reports, so this can be used to inspect its lobbies. "
                         + "Set Cap.DeferToMoreStrafts = false to take over instead.");

            // Two tick sources, and a scene hook, because this game does not
            // reliably call Update on plugin components - see the recon notes.
            // A delegate keeps firing regardless of what happens to us.
            Application.onBeforeRender += OnBeforeRender;
            SceneManager.sceneLoaded += OnSceneLoaded;

            Log.Info(Active
                ? $"{Name} v{Version} loaded - cap up to {MaxPlayers} players. "
                  + $"Press {Settings.ReportKey.Value} in a lobby for a status report."
                : $"{Name} v{Version} loaded - disabled in config, cap stays vanilla.");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // FishNet may not have existed when we loaded; keep trying until the
            // subscription takes.
            try
            {
                if (Active) TransportCap.EnsureSubscribed();
            }
            catch (Exception e)
            {
                Log.Error($"scene-load hook failed: {e}");
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

                if (Settings == null || !Settings.Enabled.Value) return;

                // The report stays available even when standing down.
                if (Settings.ReportKey.Value.IsDown() || Input.GetKeyDown(KeyCode.F9))
                    Report();
            }
            catch (Exception e)
            {
                Log.Error($"tick failed: {e}");
            }
        }

        /// <summary>
        /// Logs every value the cap depends on, so the whole chain can be checked
        /// alone - no second player needed. Each line is something that has to
        /// agree with the others for a big lobby to actually work.
        /// </summary>
        private static void Report()
        {
            try
            {
                Log.Info("---- cap report ----");
                Log.Info($"this mod active         : {Active}"
                         + (Active ? "" : " (standing down - another mod owns the cap)"));
                Log.Info($"configured ceiling      : {MaxPlayers}");

                if (!GameBridge.Usable)
                {
                    // Name them here rather than only at resolve time. That
                    // warning is emitted once, early, and scrolls away long
                    // before anyone thinks to press F9.
                    Log.Warn($"the cap is NOT being applied. Could not find: {GameBridge.MissingMembers()}");
                    Log.Info("--------------------");
                    return;
                }

                int lobbyMax = GameBridge.MaxPlayers();
                int players = GameBridge.PlayerCount();
                var dropdown = GameBridge.MaxPlayersDropdown();
                int options = GameBridge.DropdownOptionCount(dropdown);
                int expectedOptions = MaxPlayers - CapMath.MinPlayers + 1;

                Log.Info($"SteamLobby.maxPlayers   : {lobbyMax}");
                Log.Info($"players present         : {players}");
                Log.Info($"in a steam lobby        : {GameBridge.InLobby()}");
                Log.Info($"dropdown options        : {options} (expected {expectedOptions})");
                Log.Info($"max-players dropdowns   : {GameBridge.MaxPlayersDropdownReport()}");
                Log.Info($"transport clients now   : {TransportCap.CurrentTransportClients()} "
                         + $"(expected {CapMath.TransportClientsFor(lobbyMax)})");
                Log.Info($"last cap we applied     : {TransportCap.LastApplied}");
                Log.Info($"server active           : {InstanceFinder.IsServer}");

                // The verdict, so the log does not have to be read closely.
                int wanted = CapMath.TransportClientsFor(lobbyMax);
                int actual = TransportCap.CurrentTransportClients();

                if (options != expectedOptions)
                    Log.Warn($"dropdown has {options} options, expected {expectedOptions} - "
                             + "the cap cannot be selected.");

                if (actual >= 0 && actual != wanted)
                    Log.Warn($"TRANSPORT MISMATCH: accepts {actual} clients but the lobby wants "
                             + $"{wanted}. Players beyond {actual + 1} would be refused.");
                else if (actual >= 0)
                    Log.Info($"transport agrees with the lobby ({actual} clients, "
                             + $"{lobbyMax} players).");

                Log.Info("--------------------");
            }
            catch (Exception e)
            {
                Log.Error($"report failed: {e}");
            }
        }
    }
}
