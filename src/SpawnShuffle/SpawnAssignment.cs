using System.Collections.Generic;

namespace SpawnShuffle
{
    /// <summary>
    /// Which spawn point a player gets, and where around it they stand.
    /// </summary>
    public readonly struct SpawnSlot
    {
        /// <summary>Index into the map's spawn point array.</summary>
        public readonly int SpawnPointIndex;

        /// <summary>
        /// This player's place in the queue of players sharing that point.
        /// 0 means "first here"; only matters when <see cref="Occupants"/> &gt; 1.
        /// </summary>
        public readonly int Ring;

        /// <summary>How many players share this spawn point this round.</summary>
        public readonly int Occupants;

        public SpawnSlot(int spawnPointIndex, int ring, int occupants)
        {
            SpawnPointIndex = spawnPointIndex;
            Ring = ring;
            Occupants = occupants;
        }
    }

    /// <summary>
    /// Decides who spawns where each round. Deliberately pure: no Unity types,
    /// no game state, no randomness beyond the seed it is handed — so it can be
    /// reasoned about and tested off the game.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vanilla picks <c>spawnPoints[(TakeIndex + PlayerId) % count]</c>. Every
    /// player advances by one point per round, together, which means the
    /// arrangement only ever rotates: the gap between any two players is the
    /// same in round 50 as in round 1, so you fight the same neighbours all
    /// match. Worse above four players, where ids collide on the same point
    /// (there are only ever 1v1 and 4-player spawn sets in the maps) and the
    /// collisions are always between the same ids.
    /// </para>
    /// <para>
    /// This instead deals players into seats through a per-round permutation, so
    /// both the grouping and the ordering change every round.
    /// </para>
    /// <para>
    /// <b>The determinism requirement is not decorative.</b> Each player's spawn
    /// is computed in its own <c>[ServerRpc]</c> invocation, so this runs once
    /// per player, separately. If it consulted a running RNG, two players could
    /// be dealt the same seat. Everything here is therefore a pure function of
    /// (round, salt, player set): re-running it for a different player in the
    /// same round reproduces the identical deal.
    /// </para>
    /// </remarks>
    public static class SpawnAssignment
    {
        /// <summary>
        /// Works out where <paramref name="playerId"/> spawns this round.
        /// </summary>
        /// <param name="playerId">The player being spawned.</param>
        /// <param name="playerIds">
        /// Every player in the match. Order is irrelevant — it is sorted here so
        /// that all callers agree regardless of dictionary iteration order.
        /// </param>
        /// <param name="spawnPointCount">Spawn points the current map offers.</param>
        /// <param name="roundIndex">The round counter (ScoreManager.TakeIndex).</param>
        /// <param name="salt">Config value, so a match can be re-rolled.</param>
        public static SpawnSlot Assign(
            int playerId,
            IReadOnlyList<int> playerIds,
            int spawnPointCount,
            int roundIndex,
            int salt)
        {
            if (spawnPointCount <= 0) return new SpawnSlot(0, 0, 1);

            var ordered = Sorted(playerIds);
            int seatCount = ordered.Count;
            int index = ordered.IndexOf(playerId);

            // A player we don't know about (mid-round join, or an id missing
            // from the list) still needs somewhere to stand. Fall back to
            // vanilla's formula rather than dropping them at the origin.
            if (index < 0 || seatCount == 0)
                return new SpawnSlot(Mod(roundIndex + playerId, spawnPointCount), 0, 1);

            int seat = Deal(index, seatCount, Seed(roundIndex, salt));
            int spawnPointIndex = seat % spawnPointCount;

            return new SpawnSlot(spawnPointIndex, seat / spawnPointCount,
                                 OccupantsOf(spawnPointIndex, seatCount, spawnPointCount));
        }

        /// <summary>
        /// Where around a shared spawn point this player stands, as an angle in
        /// radians. Callers turn it into a position; a lone occupant gets no
        /// offset at all.
        /// </summary>
        public static double AngleFor(SpawnSlot slot, int roundIndex, int salt)
        {
            if (slot.Occupants <= 1) return 0.0;

            // Rotate the whole cluster a little each round so players don't
            // always face the same way out of a shared point.
            double phase = (Seed(roundIndex, salt ^ 0x5F37) & 0xFFFF) / 65536.0;
            return 2.0 * System.Math.PI * ((slot.Ring + phase) / slot.Occupants);
        }

        /// <summary>True when the player has to share, and so needs offsetting.</summary>
        public static bool NeedsOffset(SpawnSlot slot) => slot.Occupants > 1;

        /// <summary>
        /// Seat for one player, via a seeded Fisher-Yates deal.
        /// </summary>
        /// <remarks>
        /// The full permutation is rebuilt on every call. That is deliberate: it
        /// keeps the function pure, and at ten players the cost is nothing next
        /// to spawning a character.
        /// </remarks>
        private static int Deal(int index, int seatCount, uint seed)
        {
            var seats = new int[seatCount];
            for (int i = 0; i < seatCount; i++) seats[i] = i;

            // Walk backwards so the swap range is [0, i], the standard unbiased
            // form. NextBelow draws from our own PRNG, never UnityEngine.Random,
            // whose global state would break reproducibility across calls.
            var rng = seed;
            for (int i = seatCount - 1; i > 0; i--)
            {
                int j = (int)NextBelow(ref rng, (uint)(i + 1));
                (seats[i], seats[j]) = (seats[j], seats[i]);
            }

            return seats[index];
        }

        /// <summary>How many seats land on one spawn point, given round-robin dealing.</summary>
        private static int OccupantsOf(int spawnPointIndex, int seatCount, int spawnPointCount)
        {
            if (spawnPointIndex >= seatCount) return 0;
            return ((seatCount - 1 - spawnPointIndex) / spawnPointCount) + 1;
        }

        private static List<int> Sorted(IReadOnlyList<int> playerIds)
        {
            var ordered = new List<int>(playerIds?.Count ?? 0);
            if (playerIds != null) ordered.AddRange(playerIds);
            ordered.Sort();
            return ordered;
        }

        /// <summary>
        /// Mixes round and salt into a seed. SplitMix32 — small, well-distributed,
        /// and identical on every machine, which matters more here than quality.
        /// </summary>
        private static uint Seed(int roundIndex, int salt)
        {
            unchecked
            {
                uint x = (uint)roundIndex * 0x9E3779B9u ^ (uint)salt * 0x85EBCA6Bu;
                x ^= x >> 16; x *= 0x7FEB352Du;
                x ^= x >> 15; x *= 0x846CA68Bu;
                x ^= x >> 16;
                // Zero is a fixed point for xorshift PRNGs; nudge it off.
                return x == 0 ? 0x9E3779B9u : x;
            }
        }

        /// <summary>Draws a value in [0, bound) using rejection sampling, so the deal stays unbiased.</summary>
        private static uint NextBelow(ref uint state, uint bound)
        {
            if (bound <= 1) return 0;

            // Discard the tail that would make low values more likely.
            uint limit = uint.MaxValue - (uint.MaxValue % bound);
            uint value;
            do { value = Next(ref state); } while (value >= limit);
            return value % bound;
        }

        /// <summary>xorshift32. Deterministic, no allocation, no framework RNG.</summary>
        private static uint Next(ref uint state)
        {
            unchecked
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return state;
            }
        }

        /// <summary>Modulo that stays non-negative for negative round counters.</summary>
        private static int Mod(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }
    }
}
