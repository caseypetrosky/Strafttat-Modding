using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoreStraftsRecon
{
    /// <summary>
    /// A read-only recon tool. It patches nothing and changes no game behaviour.
    /// It exists to answer three questions:
    ///   1. What does the lobby UI hierarchy actually look like?
    ///   2. Which types and fields hold the player count?
    ///   3. What are those values at runtime, in a real lobby?
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        // GUIDs must be unique across installed mods; this also names the plugin's config file.
        public const string Guid = "com.caseypetrosky.morestraftsrecon";
        public const string Name = "MoreStrafts Recon";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log;
        internal static string DumpDir;

        private ConfigEntry<KeyboardShortcut> _keyHierarchy;
        private ConfigEntry<KeyboardShortcut> _keyScan;
        private ConfigEntry<KeyboardShortcut> _keyInspect;
        private ConfigEntry<string> _keywords;
        private ConfigEntry<string> _inspectType;
        private ConfigEntry<bool> _verboseComponents;
        private ConfigEntry<float> _autoDumpSeconds;
        private ConfigEntry<bool> _logKeyPresses;

        // Diagnostics. A hotkey that does nothing is ambiguous: the Update loop
        // might not be running, the key might not be reaching us, or the dump
        // itself might be failing. These separate the three.
        private bool _loggedHeartbeat;
        private float _autoDumpAt = -1f;
        private static string _tickSource;
        private int _lastTickFrame = -1;

        private void Awake()
        {
            Log = Logger;

            DumpDir = Path.Combine(Paths.BepInExRootPath, "recon-dumps");
            Directory.CreateDirectory(DumpDir);

            _keyHierarchy = Config.Bind(
                "Hotkeys", "DumpHierarchy",
                new KeyboardShortcut(KeyCode.F6),
                "Dump every GameObject in every loaded scene to a text file.");

            _keyScan = Config.Bind(
                "Hotkeys", "ScanTypes",
                new KeyboardShortcut(KeyCode.F7),
                "Scan Assembly-CSharp for fields/properties whose names match Recon.Keywords.");

            _keyInspect = Config.Bind(
                "Hotkeys", "InspectType",
                new KeyboardShortcut(KeyCode.F8),
                "Dump all live values of the type named in Recon.InspectType.");

            _keywords = Config.Bind(
                "Recon", "Keywords",
                "player,max,count,team,lobby,score,spawn,round,slot",
                "Comma-separated. Case-insensitive substring match on member names.");

            _inspectType = Config.Bind(
                "Recon", "InspectType",
                "GameManager",
                "Type name to inspect with the InspectType hotkey. Partial match is fine.");

            _verboseComponents = Config.Bind(
                "Recon", "VerboseComponents",
                true,
                "Include each GameObject's component list in the hierarchy dump.");

            _autoDumpSeconds = Config.Bind(
                "Recon", "AutoDumpSeconds",
                0f,
                "Seconds after each scene loads to write a hierarchy dump automatically. "
                + "0 disables it. Use this when the hotkeys don't seem to fire: set it to 10, "
                + "host a lobby, wait, and the dump appears without you pressing anything.");

            _logKeyPresses = Config.Bind(
                "Recon", "LogKeyPresses",
                false,
                "Log every function key the game sees. Turn on to check whether keyboard "
                + "input is reaching plugins at all.");

            // Used for the automatic dump. Scene load is the useful moment: it
            // is when the lobby UI exists.
            SceneManager.sceneLoaded += OnSceneLoaded;

            // A second, independent per-frame tick.
            //
            // Unity only calls Update on a component that is alive and enabled.
            // If the game destroys or disables this plugin's object after load,
            // Update goes silent - while plain C# events like sceneLoaded keep
            // firing, because they are delegates and do not care about the
            // native Unity object at all. That failure is invisible and looks
            // exactly like a broken hotkey.
            //
            // Application.onBeforeRender is a static event driven by the player
            // loop, so it keeps ticking regardless of what happened to us.
            Application.onBeforeRender += OnBeforeRender;

            Log.LogInfo($"{Name} v{Version} loaded.");
            Log.LogInfo($"Dumps will be written to: {DumpDir}");
            Log.LogInfo($"F6 = hierarchy | F7 = scan types | F8 = inspect '{_inspectType.Value}'");
            if (_autoDumpSeconds.Value > 0f)
                Log.LogInfo($"Auto-dump armed: hierarchy written {_autoDumpSeconds.Value}s after each scene load.");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ReportOwnState(scene.name);

            // Last resort. A timed dump needs something to tick; if nothing ever
            // has, waiting is pointless and the folder stays empty forever. Dump
            // right here instead - a dump taken slightly too early still proves
            // the pipeline works and is worth more than no dump at all.
            if (_tickSource == null && _autoDumpSeconds.Value > 0f)
            {
                Log.LogWarning("No per-frame tick has run yet; dumping immediately "
                               + "rather than waiting on a timer that may never fire.");
                HierarchyDumper.Dump(_verboseComponents.Value);
                return;
            }

            if (_autoDumpSeconds.Value <= 0f) return;

            // realtimeSinceStartup, not Time.time: menus and pauses can set
            // timeScale to 0, which would freeze a Time.time based delay forever.
            _autoDumpAt = Time.realtimeSinceStartup + _autoDumpSeconds.Value;
            Log.LogInfo($"Scene '{scene.name}' loaded; auto-dump in {_autoDumpSeconds.Value}s.");
        }

        // Unity's per-frame call. May stop happening; see Awake.
        private void Update() => Tick("Update");

        // The player-loop fallback, which keeps running even if we do not.
        private void OnBeforeRender() => Tick("onBeforeRender");

        /// <summary>
        /// The actual per-frame work, driven by whichever source is still alive.
        /// Both sources normally fire, so the first one each frame wins and the
        /// other returns immediately - otherwise a single keypress would be
        /// handled twice.
        /// </summary>
        private void Tick(string source)
        {
            try
            {
                if (_lastTickFrame == Time.frameCount) return;
                _lastTickFrame = Time.frameCount;

                // Proof of life, logged once. If this line never appears, the
                // plugin loaded but its Update loop is not running, and no
                // hotkey could ever work.
                if (!_loggedHeartbeat)
                {
                    _loggedHeartbeat = true;
                    _tickSource = source;
                    Log.LogInfo($"Tick is running via {source}; hotkeys are live.");
                }

                if (_logKeyPresses.Value) LogAnyFunctionKey();

                if (_autoDumpAt > 0f && Time.realtimeSinceStartup >= _autoDumpAt)
                {
                    _autoDumpAt = -1f;
                    Log.LogInfo("Auto-dump firing.");
                    HierarchyDumper.Dump(_verboseComponents.Value);
                }

                // Both the configured shortcut and the raw key. KeyboardShortcut
                // carries modifier rules that can stop it matching; the raw check
                // is the blunt fallback so a dump is always reachable.
                if (_keyHierarchy.Value.IsDown() || Input.GetKeyDown(KeyCode.F6))
                    HierarchyDumper.Dump(_verboseComponents.Value);

                if (_keyScan.Value.IsDown() || Input.GetKeyDown(KeyCode.F7))
                    TypeScanner.ScanKeywords(_keywords.Value);

                if (_keyInspect.Value.IsDown() || Input.GetKeyDown(KeyCode.F8))
                    TypeScanner.InspectType(_inspectType.Value);
            }
            catch (Exception e)
            {
                // Never let a recon crash take the game down with it.
                Log.LogError($"Recon action failed: {e}");
            }
        }

        /// <summary>
        /// Logs whether this component is still alive and enabled. Unity
        /// overloads == so a destroyed object compares equal to null while the
        /// C# reference is still perfectly usable, which is exactly how a plugin
        /// ends up with a working scene callback and a dead Update.
        /// </summary>
        private void ReportOwnState(string sceneName)
        {
            try
            {
                bool destroyed = this == null;
                string detail = destroyed
                    ? "DESTROYED (Unity object gone; Update will never run again)"
                    : $"alive, enabled={enabled}, activeInHierarchy={gameObject.activeInHierarchy}";
                Log.LogInfo($"[{sceneName}] plugin state: {detail}; tick source: {_tickSource ?? "none yet"}");
            }
            catch (Exception e)
            {
                Log.LogInfo($"[{sceneName}] plugin state unreadable ({e.GetType().Name}); "
                            + $"tick source: {_tickSource ?? "none yet"}");
            }
        }

        /// <summary>
        /// Reports any function key the game sees, so "the hotkey does nothing"
        /// can be told apart from "no keyboard input reaches plugins at all".
        /// </summary>
        private static void LogAnyFunctionKey()
        {
            for (var key = KeyCode.F1; key <= KeyCode.F12; key++)
                if (Input.GetKeyDown(key))
                    Log.LogInfo($"key seen: {key}");
        }
    }
}
