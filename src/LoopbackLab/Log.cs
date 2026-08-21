using BepInEx.Logging;

namespace LoopbackLab
{
    /// <summary>
    /// Thin wrapper so every file logs with the same prefix without passing a
    /// logger around. Safe to call before <see cref="Init"/> — messages are
    /// simply dropped rather than throwing.
    /// </summary>
    internal static class Log
    {
        private static ManualLogSource _source;

        public static void Init(ManualLogSource source) => _source = source;

        public static void Info(string message) => _source?.LogInfo(message);
        public static void Warn(string message) => _source?.LogWarning(message);
        public static void Error(string message) => _source?.LogError(message);
    }
}
