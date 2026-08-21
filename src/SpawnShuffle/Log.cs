using BepInEx.Logging;

namespace SpawnShuffle
{
    /// <summary>Shared logger, safe to call before Init (messages are dropped).</summary>
    internal static class Log
    {
        private static ManualLogSource _source;

        public static void Init(ManualLogSource source) => _source = source;

        public static void Info(string message) => _source?.LogInfo(message);
        public static void Warn(string message) => _source?.LogWarning(message);
        public static void Error(string message) => _source?.LogError(message);
    }
}
