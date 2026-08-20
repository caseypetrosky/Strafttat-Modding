using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace MoreStraftsRecon
{
    /// <summary>
    /// Reflection over the game's own assembly. This is the fast path to
    /// "where does the number 4 actually live?"
    /// </summary>
    internal static class TypeScanner
    {
        private const BindingFlags All =
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static;

        /// <summary>
        /// Finds every field and property in the game assembly whose name contains
        /// one of the keywords. For live MonoBehaviours it also reads the current value.
        /// </summary>
        public static void ScanKeywords(string csvKeywords)
        {
            var keywords = csvKeywords
                .Split(',')
                .Select(k => k.Trim().ToLowerInvariant())
                .Where(k => k.Length > 0)
                .ToArray();

            var sb = new StringBuilder();
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            sb.AppendLine($"# Keyword scan {stamp}");
            sb.AppendLine($"# Keywords: {string.Join(", ", keywords)}");
            sb.AppendLine();

            int hits = 0;

            foreach (var type in GetGameTypes())
            {
                var matches = new StringBuilder();

                foreach (var f in type.GetFields(All))
                {
                    if (!Matches(f.Name, keywords)) continue;
                    matches.AppendLine($"  field  {Pretty(f.FieldType)} {f.Name}{LiveValue(type, f)}");
                    hits++;
                }

                foreach (var p in type.GetProperties(All))
                {
                    if (!Matches(p.Name, keywords)) continue;
                    matches.AppendLine($"  prop   {Pretty(p.PropertyType)} {p.Name}");
                    hits++;
                }

                if (matches.Length > 0)
                {
                    sb.AppendLine($"### {type.FullName}");
                    sb.Append(matches);
                    sb.AppendLine();
                }
            }

            var path = Path.Combine(Plugin.DumpDir, $"scan_{stamp}.txt");
            File.WriteAllText(path, sb.ToString());
            Plugin.Log.LogInfo($"Keyword scan: {hits} matches -> {path}");
        }

        /// <summary>
        /// Dumps every field of every live instance of a type. Run this while
        /// sitting in a lobby and you get a snapshot of the real state.
        /// </summary>
        public static void InspectType(string typeName)
        {
            var matches = GetGameTypes()
                .Where(t => t.Name.IndexOf(typeName, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (matches.Count == 0)
            {
                Plugin.Log.LogWarning($"No type matching '{typeName}'. Run the F7 scan to see what exists.");
                return;
            }

            var sb = new StringBuilder();
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            sb.AppendLine($"# Inspect '{typeName}' at {stamp}");
            sb.AppendLine();

            foreach (var type in matches)
            {
                sb.AppendLine($"### {type.FullName}");

                // Static fields first - these hold constants like MAX_PLAYERS.
                foreach (var f in type.GetFields(All).Where(f => f.IsStatic))
                {
                    sb.AppendLine($"  static {f.Name} = {Format(SafeGet(f, null))}");
                }

                if (typeof(Component).IsAssignableFrom(type))
                {
                    var instances = UnityEngine.Object.FindObjectsOfType(type);
                    sb.AppendLine($"  -- {instances.Length} live instance(s) --");

                    foreach (var inst in instances)
                    {
                        sb.AppendLine($"  [{((Component)inst).gameObject.name}]");
                        foreach (var f in type.GetFields(All).Where(f => !f.IsStatic))
                            sb.AppendLine($"    {f.Name} = {Format(SafeGet(f, inst))}");
                    }
                }

                sb.AppendLine();
            }

            var path = Path.Combine(Plugin.DumpDir, $"inspect_{typeName}_{stamp}.txt");
            File.WriteAllText(path, sb.ToString());
            Plugin.Log.LogInfo($"Inspected {matches.Count} type(s) -> {path}");
        }

        private static Type[] GetGameTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name.StartsWith("Assembly-CSharp"))
                .SelectMany(SafeGetTypes)
                .ToArray();
        }

        private static Type[] SafeGetTypes(Assembly a)
        {
            // Partially-loadable assemblies throw here; keep the types that did load.
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null).ToArray(); }
            catch { return new Type[0]; }
        }

        private static bool Matches(string name, string[] keywords)
        {
            var lower = name.ToLowerInvariant();
            return keywords.Any(k => lower.Contains(k));
        }

        private static string LiveValue(Type type, FieldInfo f)
        {
            if (f.IsStatic) return $"  = {Format(SafeGet(f, null))}";
            if (!typeof(Component).IsAssignableFrom(type)) return "";

            try
            {
                var inst = UnityEngine.Object.FindObjectOfType(type);
                if (inst == null) return "";
                return $"  = {Format(SafeGet(f, inst))}";
            }
            catch { return ""; }
        }

        private static object SafeGet(FieldInfo f, object target)
        {
            try { return f.GetValue(target); }
            catch (Exception e) { return $"<error: {e.GetType().Name}>"; }
        }

        private static string Format(object v)
        {
            if (v == null) return "null";

            if (v is string s) return $"\"{s}\"";

            // Arrays and lists are the interesting case: a length-4 array is
            // exactly the kind of thing that makes a 10-player lobby hard.
            if (v is IEnumerable e && !(v is string))
            {
                var items = e.Cast<object>().Take(12).Select(x => x?.ToString() ?? "null").ToList();
                var count = e.Cast<object>().Count();
                return $"[{count}] {{ {string.Join(", ", items)}{(count > 12 ? ", ..." : "")} }}";
            }

            return v.ToString();
        }

        private static string Pretty(Type t)
        {
            if (!t.IsGenericType) return t.Name;
            var args = string.Join(", ", t.GetGenericArguments().Select(Pretty));
            return $"{t.Name.Split('`')[0]}<{args}>";
        }
    }
}
