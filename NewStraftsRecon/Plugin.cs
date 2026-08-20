using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

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
        // Change "yourname" to your handle. GUIDs must be unique across all installed mods.
        public const string Guid = "com.yourname.morestraftsrecon";
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

            Log.LogInfo($"{Name} v{Version} loaded.");
            Log.LogInfo($"Dumps will be written to: {DumpDir}");
            Log.LogInfo($"F6 = hierarchy | F7 = scan types | F8 = inspect '{_inspectType.Value}'");
        }

        private void Update()
        {
            try
            {
                if (_keyHierarchy.Value.IsDown())
                    HierarchyDumper.Dump(_verboseComponents.Value);

                if (_keyScan.Value.IsDown())
                    TypeScanner.ScanKeywords(_keywords.Value);

                if (_keyInspect.Value.IsDown())
                    TypeScanner.InspectType(_inspectType.Value);
            }
            catch (Exception e)
            {
                // Never let a recon crash take the game down with it.
                Log.LogError($"Recon action failed: {e}");
            }
        }
    }
}
