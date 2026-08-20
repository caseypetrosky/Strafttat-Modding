using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoreStraftsRecon
{
    /// <summary>
    /// Walks every loaded scene and writes the full GameObject tree to a file.
    /// This is how you find out whether the four lobby player slots are prefab
    /// instances you can clone, or objects hand-placed in the scene.
    /// </summary>
    internal static class HierarchyDumper
    {
        public static void Dump(bool includeComponents)
        {
            var sb = new StringBuilder();
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

            sb.AppendLine($"# Hierarchy dump {stamp}");
            sb.AppendLine($"# Active scene: {SceneManager.GetActiveScene().name}");
            sb.AppendLine();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                sb.AppendLine($"=== SCENE: {scene.name} ({scene.rootCount} roots) ===");
                foreach (var root in scene.GetRootGameObjects())
                    WriteObject(sb, root.transform, 0, includeComponents);
                sb.AppendLine();
            }

            // DontDestroyOnLoad objects live in their own hidden scene. Managers
            // and persistent UI usually end up here, so we very much want them.
            var ddol = GetDontDestroyOnLoadScene();
            if (ddol.HasValue && ddol.Value.IsValid())
            {
                sb.AppendLine("=== SCENE: DontDestroyOnLoad ===");
                foreach (var root in ddol.Value.GetRootGameObjects())
                    WriteObject(sb, root.transform, 0, includeComponents);
            }

            var path = Path.Combine(Plugin.DumpDir, $"hierarchy_{stamp}.txt");
            File.WriteAllText(path, sb.ToString());
            Plugin.Log.LogInfo($"Wrote hierarchy dump: {path}");
        }

        private static void WriteObject(StringBuilder sb, Transform t, int depth, bool includeComponents)
        {
            var indent = new string(' ', depth * 2);
            var active = t.gameObject.activeSelf ? "" : " [INACTIVE]";
            sb.AppendLine($"{indent}{t.name}{active}");

            if (includeComponents)
            {
                foreach (var c in t.GetComponents<Component>())
                {
                    if (c == null)
                    {
                        sb.AppendLine($"{indent}  <MISSING SCRIPT>");
                        continue;
                    }

                    var line = $"{indent}  <{c.GetType().Name}>";

                    // Grab text content generically so this works for both
                    // UnityEngine.UI.Text and TextMeshPro without referencing either.
                    var text = TryGetText(c);
                    if (!string.IsNullOrEmpty(text))
                        line += $" text=\"{text.Replace("\n", "\\n")}\"";

                    sb.AppendLine(line);
                }
            }

            for (int i = 0; i < t.childCount; i++)
                WriteObject(sb, t.GetChild(i), depth + 1, includeComponents);
        }

        private static string TryGetText(Component c)
        {
            try
            {
                var prop = c.GetType().GetProperty("text",
                    BindingFlags.Public | BindingFlags.Instance);

                if (prop != null && prop.PropertyType == typeof(string))
                    return prop.GetValue(c) as string;
            }
            catch { /* some properties throw on access; not our problem */ }

            return null;
        }

        private static Scene? GetDontDestroyOnLoadScene()
        {
            GameObject probe = null;
            try
            {
                probe = new GameObject("__recon_probe");
                UnityEngine.Object.DontDestroyOnLoad(probe);
                return probe.scene;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (probe != null) UnityEngine.Object.Destroy(probe);
            }
        }
    }
}
