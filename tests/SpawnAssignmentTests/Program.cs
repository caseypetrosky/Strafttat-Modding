using System;
using System.Collections.Generic;
using System.Linq;
using SpawnShuffle;

class Program
{
    static int failures = 0;
    static void Check(bool cond, string what)
    {
        if (!cond) { Console.WriteLine($"  FAIL: {what}"); failures++; }
    }

    static void Main()
    {
        // 1. Bijection: every player gets a distinct seat -> distinct (point,ring).
        Console.WriteLine("1. deal is a bijection (no two players share a slot)");
        foreach (int P in new[] { 2, 3, 4, 5, 7, 8, 10 })
        foreach (int N in new[] { 2, 4 })
        {
            var players = Enumerable.Range(0, P).ToList();
            for (int round = 0; round < 200; round++)
            {
                var seen = new HashSet<(int, int)>();
                foreach (int p in players)
                {
                    var s = SpawnAssignment.Assign(p, players, N, round, 0);
                    Check(s.SpawnPointIndex >= 0 && s.SpawnPointIndex < N, $"point in range P={P} N={N}");
                    Check(seen.Add((s.SpawnPointIndex, s.Ring)), $"unique slot P={P} N={N} round={round}");
                }
            }
        }

        // 2. Purity: same inputs -> same output, however many times we ask.
        //    This is the property the per-player ServerRpc design depends on.
        Console.WriteLine("2. repeated calls agree (separate RPC invocations)");
        var ps = Enumerable.Range(0, 7).ToList();
        for (int round = 0; round < 50; round++)
            foreach (int p in ps)
            {
                var a = SpawnAssignment.Assign(p, ps, 4, round, 3);
                for (int again = 0; again < 5; again++)
                {
                    var b = SpawnAssignment.Assign(p, ps, 4, round, 3);
                    Check(a.SpawnPointIndex == b.SpawnPointIndex && a.Ring == b.Ring, "stable across calls");
                }
            }

        // 2b. Order of the player list must not matter (dictionary iteration order).
        Console.WriteLine("2b. player-list order does not change the deal");
        var shuffled = new List<int>(ps); shuffled.Reverse();
        foreach (int p in ps)
        {
            var a = SpawnAssignment.Assign(p, ps, 4, 11, 3);
            var b = SpawnAssignment.Assign(p, shuffled, 4, 11, 3);
            Check(a.SpawnPointIndex == b.SpawnPointIndex && a.Ring == b.Ring, "order-independent");
        }

        // 3. Occupants must equal the real number of players on that point.
        Console.WriteLine("3. Occupants matches the actual crowd");
        foreach (int P in new[] { 3, 5, 6, 9, 10 })
        {
            var players = Enumerable.Range(0, P).ToList();
            for (int round = 0; round < 100; round++)
            {
                var counts = new Dictionary<int, int>();
                var claimed = new Dictionary<int, int>();
                foreach (int p in players)
                {
                    var s = SpawnAssignment.Assign(p, players, 4, round, 0);
                    counts[s.SpawnPointIndex] = counts.GetValueOrDefault(s.SpawnPointIndex) + 1;
                    claimed[s.SpawnPointIndex] = s.Occupants;
                }
                foreach (var kv in counts)
                    Check(kv.Value == claimed[kv.Key], $"occupants P={P} point={kv.Key} real={kv.Value} claimed={claimed[kv.Key]}");
            }
        }

        // 4. THE POINT OF THE MOD: neighbours must actually change round to round.
        //    Vanilla is (round + id) % N, where the gap between two players is
        //    constant forever. Measure how often a pair shares a spawn point.
        Console.WriteLine("4. neighbours change between rounds");
        foreach (int P in new[] { 5, 8, 10 })
        {
            var players = Enumerable.Range(0, P).ToList();
            int changes = 0, comparisons = 0;
            var prev = new Dictionary<int, int>();
            for (int round = 0; round < 300; round++)
            {
                var cur = players.ToDictionary(p => p, p => SpawnAssignment.Assign(p, players, 4, round, 0).SpawnPointIndex);
                if (round > 0)
                    foreach (var a in players) foreach (var b in players)
                    {
                        if (a >= b) continue;
                        bool wasTogether = prev[a] == prev[b];
                        bool isTogether = cur[a] == cur[b];
                        if (wasTogether != isTogether) changes++;
                        comparisons++;
                    }
                prev = cur;
            }
            double rate = 100.0 * changes / comparisons;
            Console.WriteLine($"   P={P}: pairing changed in {rate:F1}% of pair-rounds");
            Check(rate > 10.0, $"neighbours actually rotate at P={P}");
        }

        // 4b. Vanilla comparison: prove the problem is real, not imagined.
        Console.WriteLine("4b. vanilla formula for contrast");
        foreach (int P in new[] { 5, 8, 10 })
        {
            int changes = 0, comparisons = 0;
            var players = Enumerable.Range(0, P).ToList();
            var prev = new Dictionary<int, int>();
            for (int round = 0; round < 300; round++)
            {
                var cur = players.ToDictionary(p => p, p => (round + p) % 4);
                if (round > 0)
                    foreach (var a in players) foreach (var b in players)
                    {
                        if (a >= b) continue;
                        if ((prev[a] == prev[b]) != (cur[a] == cur[b])) changes++;
                        comparisons++;
                    }
                prev = cur;
            }
            Console.WriteLine($"   P={P}: vanilla pairing changed in {100.0 * changes / comparisons:F1}% of pair-rounds");
        }

        // 4c. Small lobbies (players <= spawn points) are the common case: nobody
        //     shares a point, so "who shares" says nothing. The question there is
        //     whether the ARRANGEMENT varies - measured as the relative offset
        //     (slotA - slotB) mod N. Vanilla holds it constant forever.
        Console.WriteLine("4c. small lobbies: relative arrangement varies");
        foreach (int P in new[] { 2, 3, 4 })
        {
            const int N = 4;
            var players = Enumerable.Range(0, P).ToList();
            var distinct = new Dictionary<(int, int), HashSet<int>>();
            var vanillaDistinct = new Dictionary<(int, int), HashSet<int>>();
            for (int round = 0; round < 400; round++)
                foreach (var a in players) foreach (var b in players)
                {
                    if (a >= b) continue;
                    var key = (a, b);
                    if (!distinct.ContainsKey(key)) { distinct[key] = new(); vanillaDistinct[key] = new(); }
                    int sa = SpawnAssignment.Assign(a, players, N, round, 0).SpawnPointIndex;
                    int sb = SpawnAssignment.Assign(b, players, N, round, 0).SpawnPointIndex;
                    distinct[key].Add(((sa - sb) % N + N) % N);
                    vanillaDistinct[key].Add((((round + a) % N - (round + b) % N) % N + N) % N);
                }
            double avg = distinct.Values.Average(h => h.Count);
            double vAvg = vanillaDistinct.Values.Average(h => h.Count);
            Console.WriteLine($"   P={P}: distinct relative positions - vanilla {vAvg:F1}, shuffle {avg:F1} (max {N - 1})");
            Check(avg > vAvg, $"shuffle varies arrangement more than vanilla at P={P}");
            Check(avg >= N - 1 - 1e-9, $"shuffle reaches every arrangement at P={P}");
        }

        // 4d. Regression: with fewer players than spawn points, every point on the
        //     map must still get used. An earlier version mapped seats straight
        //     onto points, so a 3-player match never once used the 4th spawn.
        Console.WriteLine("4d. every spawn point gets used");
        foreach (int P in new[] { 2, 3, 4, 5 })
            foreach (int N in new[] { 4, 6 })
            {
                var players = Enumerable.Range(0, P).ToList();
                var used = new HashSet<int>();
                for (int round = 0; round < 400; round++)
                    foreach (int p in players)
                        used.Add(SpawnAssignment.Assign(p, players, N, round, 0).SpawnPointIndex);
                Check(used.Count == N, $"P={P} N={N} used only {used.Count}/{N} spawn points");
            }

        // 5. Angles stay inside one turn and a lone player is never displaced.
        Console.WriteLine("5. offsets sane");
        for (int round = 0; round < 100; round++)
        {
            var solo = SpawnAssignment.Assign(0, new List<int> { 0 }, 4, round, 0);
            Check(!SpawnAssignment.NeedsOffset(solo), "lone player not offset");
            Check(SpawnAssignment.AngleFor(solo, round, 0) == 0.0, "lone angle zero");

            var many = Enumerable.Range(0, 10).ToList();
            foreach (int p in many)
            {
                var s = SpawnAssignment.Assign(p, many, 4, round, 0);
                double ang = SpawnAssignment.AngleFor(s, round, 0);
                Check(ang >= 0.0 && ang < 2 * Math.PI + 1e-9, "angle within a turn");
            }
        }

        // 6. Degenerate inputs must not throw.
        Console.WriteLine("6. edge cases");
        var none = new List<int>();
        var z = SpawnAssignment.Assign(0, none, 4, 5, 0);
        Check(z.SpawnPointIndex is >= 0 and < 4, "empty roster falls back in range");
        var noPoints = SpawnAssignment.Assign(0, ps, 0, 5, 0);
        Check(noPoints.SpawnPointIndex == 0, "zero spawn points survives");
        var unknown = SpawnAssignment.Assign(999, ps, 4, 5, 0);
        Check(unknown.SpawnPointIndex is >= 0 and < 4, "unknown player falls back in range");
        var neg = SpawnAssignment.Assign(999, ps, 4, -3, 0);
        Check(neg.SpawnPointIndex is >= 0 and < 4, "negative round stays in range");

        Console.WriteLine(failures == 0 ? "\nALL PASS" : $"\n{failures} FAILURES");
        Environment.Exit(failures == 0 ? 0 : 1);
    }
}
