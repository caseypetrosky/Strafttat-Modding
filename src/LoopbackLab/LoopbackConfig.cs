using BepInEx.Configuration;
using UnityEngine;

namespace LoopbackLab
{
    /// <summary>
    /// All tunables in one place, bound once at startup.
    /// </summary>
    /// <remarks>
    /// <see cref="Enabled"/> is read during FishNet's initialisation, which
    /// happens once and cannot be redone — see <see cref="TransportInstaller"/>.
    /// Changing it therefore requires a game restart, and the config
    /// description says so. Every other setting is read on demand.
    /// </remarks>
    internal sealed class LoopbackConfig
    {
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<int> Port;
        public readonly ConfigEntry<int> MaximumClients;
        public readonly ConfigEntry<string> ClientAddress;
        public readonly ConfigEntry<KeyboardShortcut> HostKey;
        public readonly ConfigEntry<KeyboardShortcut> JoinKey;
        public readonly ConfigEntry<KeyboardShortcut> StopKey;

        public LoopbackConfig(ConfigFile config)
        {
            Enabled = config.Bind(
                "Loopback", "Enabled", false,
                "Replace the Steam transport with FishNet's Tugboat (plain TCP/UDP) so "
                + "several instances on this machine can play together. "
                + "REQUIRES A RESTART: the transport is chosen once, while the game boots. "
                + "Leave false for normal online play.");

            Port = config.Bind(
                "Loopback", "Port", 7770,
                new ConfigDescription(
                    "TCP/UDP port the loopback host listens on. All instances must agree.",
                    new AcceptableValueRange<int>(1024, 65535)));

            MaximumClients = config.Bind(
                "Loopback", "MaximumClients", 16,
                new ConfigDescription(
                    "Transport-level client cap for the loopback host. The host itself is "
                    + "not counted, so 16 leaves plenty of room for 10-player testing.",
                    new AcceptableValueRange<int>(1, 4095)));

            ClientAddress = config.Bind(
                "Loopback", "ClientAddress", "127.0.0.1",
                "Address joining instances connect to. Keep 127.0.0.1 for same-machine "
                + "testing; a LAN IP works too if you test across two PCs.");

            HostKey = config.Bind(
                "Hotkeys", "Host", new KeyboardShortcut(KeyCode.F9),
                "Start a loopback server plus a local client on this instance.");

            JoinKey = config.Bind(
                "Hotkeys", "Join", new KeyboardShortcut(KeyCode.F10),
                "Connect this instance to a loopback host at ClientAddress.");

            StopKey = config.Bind(
                "Hotkeys", "Stop", new KeyboardShortcut(KeyCode.F11),
                "Disconnect this instance's client and, if hosting, stop the server.");
        }
    }
}
