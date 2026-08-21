# Spawn Shuffle — test notes

```bash
cd tests/SpawnAssignmentTests && dotnet run
```

Exit code 0 means everything passed. No packages, no Unity, no game required:
`SpawnShuffle.SpawnAssignment` is a pure function, so it can be exercised
directly as a console app.

## What is checked, and why each matters

**1. The deal is a bijection.** No two players may land on the same
(spawn point, ring) pair. Checked for 2–10 players over 2 and 4 spawn points,
200 rounds each. A duplicate here would mean two players spawning inside each
other.

**2. Repeated calls agree.** The same inputs must give the same answer every
time. This is the property the whole design rests on: spawning happens inside a
`[ServerRpc]`, so the function runs **once per player, in separate
invocations**. Anything stateful — a running RNG, a cached partial deal — could
hand two players the same seat. The test asks five times per player per round
and demands identical answers.

**2b. Player-list order is irrelevant.** The roster arrives from a dictionary,
whose iteration order is not guaranteed. The list is sorted internally; this
test feeds it reversed and requires the same deal.

**3. `Occupants` is truthful.** The count each player is told about their spawn
point must match how many players actually landed there, since it drives how
they are spread around it. An undercount would stack players on top of one
another.

**4. Neighbours actually change.** The point of the mod. Over 300 rounds, count
how often a given pair flips between sharing and not sharing a spawn point:

| Players | Vanilla `(round + id) % 4` | Spawn Shuffle |
|---|---|---|
| 5  | 0.0% | 17.8% |
| 8  | 0.0% | 24.2% |
| 10 | 0.0% | 28.9% |

**4b** runs vanilla's formula through the identical measurement, and it scores a
flat zero at every player count — because every player advances one point per
round together, so the gap between any two of them is fixed for the whole match.
That number is the justification for this plugin existing; it is measured, not
assumed.

**5. Offsets are sane.** A player alone at a point is never displaced (offset
exactly zero), and every angle falls within one full turn.

**6. Degenerate input never throws.** Empty roster, zero spawn points, an
unknown player id, and a negative round counter all return an in-range slot
instead of an exception. In the plugin these paths also fall through to vanilla
spawning, so the worst case is stock behaviour.

## What these tests do *not* cover

The Harmony patch itself — whether the prefix is reached at round start, whether
chosen positions land in walkable geometry, and whether `ClusterRadius` looks
right with 6+ players — needs the game. `SpawnAssignment` is separated from
`RoundSpawnPatch` precisely so the decision logic could be verified without it,
but the integration still has to be confirmed in a real match.
