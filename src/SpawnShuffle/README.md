# Spawn Shuffle

Re-deals spawn points every round, so you stop starting next to the same players
all match.

## The problem

Vanilla picks a spawn like this (`PlayerManager.cs:352`):

```csharp
spawnPoint = CurrentSpawnPoints[(ScoreManager.Instance.TakeIndex + ClientScript.PlayerId) % CurrentSpawnPoints.Length];
```

`TakeIndex` is the round counter, so every player advances one spawn point per
round — **together**. The whole arrangement rotates, but the *gap* between any
two players never changes. Player 2 and player 3 are adjacent in round 1 and
still adjacent in round 50.

Above 4 players it gets worse: maps only ship two spawn sets, tagged
`Spawnpoints` (1v1) and `Spawnpoints4Player`, so ids collide under `% 4` — and
because the formula is fixed, the *same pairs* collide every single round.

Measured over 300 rounds, how often a given pair of players changes from
sharing/not-sharing a spawn point:

| Players | Vanilla | Spawn Shuffle |
|---|---|---|
| 5  | 0.0% | 17.8% |
| 8  | 0.0% | 24.2% |
| 10 | 0.0% | 28.9% |

Vanilla is not "rarely" — it is *never*.

**At 4 players or fewer** nobody shares a point, so the question is instead
whether the *arrangement* changes — measured as how many distinct relative
positions a given pair ever occupies over 400 rounds (3 is the most possible
when nobody shares):

| Players (4 spawn points) | Vanilla | Spawn Shuffle |
|---|---|---|
| 2  | 1.0 | 3.0 |
| 3  | 1.0 | 3.0 |
| 4  | 1.0 | 3.0 |

Vanilla sees exactly **one** arrangement, forever: if someone starts one spawn
point away from you in round 1, they are one spawn point away for the rest of
the match. This reaches every arrangement available.

## What this does

Each round, players are dealt into seats through a permutation seeded by the
round number, and the map's spawn points are shuffled independently. Seats then
map round-robin onto that shuffled order, so who shares a point, the order
around it, and which part of the map is used all change from round to round.

Shuffling the points as well as the players is what keeps small lobbies honest:
mapping seats straight onto points would confine a 3-player match to the first
three spawns (never using the fourth) and would always leave two players on
adjacent points rather than sometimes opposite ones.

Where more players than points exist, everyone sharing a point is spread evenly
around a small circle (`ClusterRadius`, default 0.6 m), rotated a little each
round so the cluster isn't always oriented the same way.

## Config

`BepInEx/config/com.caseypetrosky.spawnshuffle.cfg`

| Setting | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Off restores vanilla spawn order |
| `MinimumPlayers` | `3` | Smaller matches are left alone |
| `OverrideTeamModes` | `false` | Team modes keep the game's hand-tuned 2v2 placement |
| `ClusterRadius` | `0.6` | Metres apart when players share a point |
| `Salt` | `0` | Change for a different sequence; same salt + round = same deal |

## Design notes

**Where it patches.** `PlayerManager` has two `SpawnPlayer` overloads. The
four-argument one (`suit, cig, position, rotation`) just places a body, and
`CmdRespawn` uses it for mid-round respawns via `ReturnSpawnPoint`. Patching
that would hijack respawns as well. The round-start *decision* is made in the
two-argument overload, so that is the one taken over — respawns are untouched.

**Why the logic is a pure function.** Spawning happens inside a `[ServerRpc]`,
which means it runs on the server once per player, in separate invocations. A
running RNG would deal two players the same seat. So `SpawnAssignment` takes
`(playerId, playerIds, spawnPointCount, roundIndex, salt)` and nothing else,
rebuilding the whole permutation each call — at ten players that costs nothing
next to spawning a character. It uses its own xorshift PRNG rather than
`UnityEngine.Random`, whose global state would break reproducibility.

**Failure is always vanilla.** Every bail-out path returns `true` so the
original method runs. A missing field, an unknown player, a map with no spawn
points, or an outright exception all degrade to stock behaviour rather than
leaving someone unspawned. Errors log once, not every spawn.

**No `Assembly-CSharp` reference.** All game access goes through `GameAccess`
using `AccessTools`, resolved once and cached. A renamed field produces a
warning naming exactly what is missing, and the plugin stands down.

## Interaction with MoreStrafts_UISpawnAddon

Worth knowing: **moreStrafts contains no spawn code whatsoever** — the circular
spawn offsets people attribute to it come from the separate UISpawnAddon, which
prefixes the *four-argument* `SpawnPlayer` and adds `playerId * 36°` at a fixed
0.5 m.

Since that patch sits on a different method, both apply: this plugin chooses the
spawn point and cluster position, then the addon adds its fixed per-id nudge on
top. That does not undo the re-dealing — grouping is still decided here — but
the two offsets stack, so players may end up slightly further apart than
`ClusterRadius` suggests. If it looks wrong with both installed, lower
`ClusterRadius` (0.3–0.4 works out to roughly the same total spread).

None of the addon's code was reused here; it has no public source and no
license. It was read only to understand the existing behaviour.

## Status

The assignment logic is **unit-tested off the game** (see
`docs/SPAWN-SHUFFLE-TESTS.md`): the deal is verified to be a bijection, stable
across repeated calls, independent of player-list order, correct in its occupant
counts, and safe on degenerate input. The measured table above comes from those
tests.

The Harmony patch itself is compile-verified but **has not been run in-game**.
First-run checks: that the prefix is reached at round start, that positions land
inside the map rather than in geometry, and that `ClusterRadius` looks sane with
6+ players.
