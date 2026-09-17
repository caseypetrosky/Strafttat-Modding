// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;

namespace StraftatCap
{
    /// <summary>
    /// The arithmetic behind the lobby player cap, with nothing Unity or
    /// game-specific in it, so every rule here can be tested off the game.
    /// </summary>
    /// <remarks>
    /// Each rule below mirrors something verified in the game's own source
    /// rather than guessed at; the comments name what.
    /// </remarks>
    public static class CapMath
    {
        /// <summary>Vanilla's smallest lobby. A 1v1 still needs two people.</summary>
        public const int MinPlayers = 2;

        /// <summary>
        /// Ceiling this mod raises the cap to. Ten is what the lobby UI and the
        /// round-end screen can be made to hold; going higher is a UI problem,
        /// not an arithmetic one.
        /// </summary>
        public const int MaxPlayers = 10;

        /// <summary>
        /// Player count for a dropdown index.
        /// </summary>
        /// <remarks>
        /// Vanilla: <c>maxPlayers = _dropdown.value + 2</c>
        /// (SteamLobby.SetMaxPlayers). The mod must keep this exact
        /// relationship, because vanilla's own handler still runs and will
        /// recompute the value this way.
        /// </remarks>
        public static int PlayersForDropdownIndex(int index) => index + MinPlayers;

        /// <summary>The dropdown index that selects a given player count.</summary>
        public static int DropdownIndexForPlayers(int players) =>
            Clamp(players) - MinPlayers;

        /// <summary>
        /// Labels for the expanded dropdown, one per selectable count.
        /// </summary>
        public static List<string> DropdownOptions(int maxPlayers)
        {
            var options = new List<string>();
            for (int players = MinPlayers; players <= Clamp(maxPlayers); players++)
                options.Add($"{players} Players");
            return options;
        }

        /// <summary>
        /// Transport client allowance for a player count.
        /// </summary>
        /// <remarks>
        /// One fewer than the player count: the host occupies a player slot but
        /// is not a connected client. Vanilla does the same, with the comment
        /// "-1 because the host is not counted as a player LOL".
        /// </remarks>
        public static int TransportClientsFor(int maxPlayers) => Clamp(maxPlayers) - 1;

        /// <summary>Keeps a player count inside the supported range.</summary>
        public static int Clamp(int players)
        {
            if (players < MinPlayers) return MinPlayers;
            if (players > MaxPlayers) return MaxPlayers;
            return players;
        }

        /// <summary>
        /// Whether a client at <paramref name="playerIndex"/> would kick itself.
        /// </summary>
        /// <remarks>
        /// Vanilla's Update kicks any player whose index is at or beyond
        /// maxPlayers (SteamLobby, "Kick extra players"). That check runs
        /// against each client's *own* copy of maxPlayers, so a client left on a
        /// stale value drops itself out of a lobby the host thinks is fine.
        /// This is why the cap has to reach every client, not just the host.
        /// </remarks>
        public static bool WouldSelfKick(int playerIndex, int clientMaxPlayers) =>
            playerIndex >= clientMaxPlayers;

        /// <summary>
        /// Whether vanilla will accept a cap update naming this many players.
        /// </summary>
        /// <remarks>
        /// UpdateOnClients starts with
        /// <c>if (players.Count > maxPlayers) return;</c> - a cap lower than the
        /// crowd already present is dropped on the floor rather than applied.
        /// Shrinking a full lobby therefore silently does nothing.
        /// </remarks>
        public static bool UpdateWouldApply(int currentPlayerCount, int proposedMaxPlayers) =>
            currentPlayerCount <= proposedMaxPlayers;
    }
}
