// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using StraftatModding;

namespace StraftatCap
{
    /// <summary>
    /// Every reach into the game's own types, in one place.
    /// </summary>
    /// <remarks>
    /// Reflection rather than a compile-time reference to Assembly-CSharp or
    /// TextMeshPro. Two reasons: a renamed member degrades to a logged warning
    /// and vanilla behaviour instead of a plugin that will not load at all, and
    /// it keeps the whole plugin buildable and verifiable without the game's
    /// assemblies to hand.
    ///
    /// Only simple signatures are used - ClearOptions(), AddOptions(List&lt;string&gt;),
    /// the int 'value' property - so nothing here depends on TMP's own types.
    /// Members are resolved once and cached.
    /// </remarks>
    internal static class GameBridge
    {
        private static bool _resolved;
        private static bool _usable;

        private static Func<object> _lobby;
        private static FieldInfo _maxPlayers;
        private static FieldInfo _maxPlayersDropdown;
        private static FieldInfo _players;
        private static FieldInfo _inSteamLobby;

        private static MethodInfo _clearOptions;
        private static MethodInfo _addOptions;
        private static PropertyInfo _dropdownValue;
        private static MethodInfo _refreshShownValue;

        public static bool Usable
        {
            get
            {
                if (!_resolved) Resolve();
                return _usable;
            }
        }

        private static void Resolve()
        {
            _resolved = true;
            try
            {
                var steamLobby = AccessTools.TypeByName("SteamLobby");
                if (steamLobby == null)
                {
                    Log.Warn("SteamLobby type not found; standing down, cap stays vanilla.");
                    return;
                }

                // Field or property - STRAFTAT uses a plain static field here.
                _lobby = StaticAccess.Getter(steamLobby, "Instance");
                _maxPlayers = AccessTools.Field(steamLobby, "maxPlayers");
                _maxPlayersDropdown = AccessTools.Field(steamLobby, "MaxPlayersDropdown");
                _players = AccessTools.Field(steamLobby, "players");
                _inSteamLobby = AccessTools.Field(steamLobby, "inSteamLobby");

                // TMP_Dropdown, reached by name so TextMeshPro need not be referenced.
                var dropdown = AccessTools.TypeByName("TMPro.TMP_Dropdown");
                if (dropdown != null)
                {
                    _clearOptions = AccessTools.Method(dropdown, "ClearOptions");
                    _addOptions = AccessTools.Method(dropdown, "AddOptions", new[] { typeof(List<string>) });
                    _dropdownValue = AccessTools.Property(dropdown, "value");
                    _refreshShownValue = AccessTools.Method(dropdown, "RefreshShownValue");
                }

                _usable = _lobby != null && _maxPlayers != null
                          && _maxPlayersDropdown != null && _clearOptions != null
                          && _addOptions != null && _dropdownValue != null;

                if (_usable) Log.Info("game members resolved.");
                else Log.Warn($"game layout changed ({Missing()}); standing down, cap stays vanilla.");
            }
            catch (Exception e)
            {
                Log.Error($"resolving game members failed: {e}");
                _usable = false;
            }
        }

        /// <summary>Which members could not be found, for reporting.</summary>
        public static string MissingMembers() { if (!_resolved) Resolve(); return Missing(); }

        private static string Missing()
        {
            var missing = new List<string>();
            if (_lobby == null) missing.Add("SteamLobby.Instance");
            if (_maxPlayers == null) missing.Add("SteamLobby.maxPlayers");
            if (_maxPlayersDropdown == null) missing.Add("SteamLobby.MaxPlayersDropdown");
            if (_clearOptions == null) missing.Add("TMP_Dropdown.ClearOptions");
            if (_addOptions == null) missing.Add("TMP_Dropdown.AddOptions(List<string>)");
            if (_dropdownValue == null) missing.Add("TMP_Dropdown.value");
            return string.Join(", ", missing);
        }

        /// <summary>The live SteamLobby, or null before one exists.</summary>
        public static object Lobby() => _lobby?.Invoke();

        /// <summary>The lobby's current player cap, or 0 if unavailable.</summary>
        public static int MaxPlayers()
        {
            var lobby = Lobby();
            return lobby == null ? 0 : (int)_maxPlayers.GetValue(lobby);
        }

        /// <summary>How many players are in the lobby right now.</summary>
        public static int PlayerCount()
        {
            var lobby = Lobby();
            if (lobby == null || _players == null) return 0;
            return _players.GetValue(lobby) is System.Collections.ICollection list ? list.Count : 0;
        }

        /// <summary>Whether we are actually in a Steam lobby.</summary>
        public static bool InLobby()
        {
            var lobby = Lobby();
            return lobby != null && _inSteamLobby != null && (bool)_inSteamLobby.GetValue(lobby);
        }

        /// <summary>The max-players dropdown, or null.</summary>
        public static object MaxPlayersDropdown()
        {
            var lobby = Lobby();
            return lobby == null ? null : _maxPlayersDropdown.GetValue(lobby);
        }

        /// <summary>
        /// Rewrites the dropdown's options and restores the current selection.
        /// </summary>
        /// <returns>True if the dropdown was rebuilt.</returns>
        public static bool ExpandDropdown(object dropdown, int maxPlayers)
        {
            if (dropdown == null) return false;

            // Keep what the player had chosen. ClearOptions resets the value, and
            // vanilla derives the real cap straight from this index, so losing it
            // would silently change the lobby size.
            int previous = (int)_dropdownValue.GetValue(dropdown);

            _clearOptions.Invoke(dropdown, null);
            _addOptions.Invoke(dropdown, new object[] { CapMath.DropdownOptions(maxPlayers) });

            int restored = Math.Min(previous, CapMath.DropdownIndexForPlayers(maxPlayers));
            _dropdownValue.SetValue(dropdown, restored);
            _refreshShownValue?.Invoke(dropdown, null);
            return true;
        }

        /// <summary>
        /// Expands every max-players dropdown in the loaded scenes, not just the
        /// one SteamLobby holds a reference to.
        /// </summary>
        /// <remarks>
        /// <para>
        /// There are two of them. <c>SteamLobby.MaxPlayersDropdown</c> points at
        /// <c>MaxPlayersOutsideLobby</c>, used on the pre-lobby screen; a second
        /// dropdown named <c>MaxPlayers</c> lives inside the lobby window. A
        /// <c>ChangeOtherDropdownValue</c> component copies the selected index
        /// from whichever one is touched to the other.
        /// </para>
        /// <para>
        /// Expanding only the referenced one leaves the in-lobby dropdown with
        /// vanilla's three options, and that is the one actually reachable once
        /// you are in a lobby - the outside one is deactivated. So the host
        /// could not choose more than four from inside the lobby, and any use of
        /// it pushed an index of at most 2 back through the sync, dropping the
        /// cap to four players with no warning.
        /// </para>
        /// <para>
        /// Because the sync copies the raw index, both dropdowns must also offer
        /// the same options or the index would mean different things on each.
        /// </para>
        /// </remarks>
        /// <returns>How many dropdowns were expanded.</returns>
        public static int ExpandAllMaxPlayersDropdowns(int maxPlayers)
        {
            var dropdownType = AccessTools.TypeByName("TMPro.TMP_Dropdown");
            if (dropdownType == null) return 0;

            int expanded = 0;

            // FindObjectsOfTypeAll rather than FindObjectsOfType: inside a lobby
            // the outside-lobby dropdown is inactive, and the plain call skips
            // inactive objects.
            foreach (var candidate in UnityEngine.Resources.FindObjectsOfTypeAll(dropdownType))
            {
                var component = candidate as UnityEngine.Component;
                if (component == null) continue;

                var go = component.gameObject;

                // Assets and prefabs report an invalid scene; only touch live ones.
                if (!go.scene.IsValid()) continue;
                if (!go.name.StartsWith("MaxPlayers", StringComparison.Ordinal)) continue;

                if (ExpandDropdown(component, maxPlayers))
                {
                    expanded++;
                    Log.Info($"expanded dropdown '{go.name}' to {CapMath.MinPlayers}-{maxPlayers}.");
                }
            }

            return expanded;
        }

        /// <summary>
        /// Every max-players dropdown with its option count, for the report.
        /// Two are expected; seeing one means the in-lobby copy was missed and
        /// the host would be stuck at vanilla's four from inside a lobby.
        /// </summary>
        public static string MaxPlayersDropdownReport()
        {
            var dropdownType = AccessTools.TypeByName("TMPro.TMP_Dropdown");
            if (dropdownType == null) return "TMP_Dropdown type not found";

            var parts = new List<string>();
            foreach (var candidate in UnityEngine.Resources.FindObjectsOfTypeAll(dropdownType))
            {
                var component = candidate as UnityEngine.Component;
                if (component == null) continue;

                var go = component.gameObject;
                if (!go.scene.IsValid()) continue;
                if (!go.name.StartsWith("MaxPlayers", StringComparison.Ordinal)) continue;

                parts.Add($"{go.name}={DropdownOptionCount(component)}"
                          + (go.activeInHierarchy ? "" : " (inactive)"));
            }

            return parts.Count == 0 ? "none found" : string.Join(", ", parts);
        }

        /// <summary>How many options the dropdown currently offers.</summary>
        public static int DropdownOptionCount(object dropdown)
        {
            if (dropdown == null) return 0;
            var options = AccessTools.Property(dropdown.GetType(), "options")?.GetValue(dropdown);
            return options is System.Collections.ICollection list ? list.Count : 0;
        }
    }
}
