# Spawn Shuffle

Stops you spawning next to the same person every round.

## The problem

STRAFTAT picks your spawn with `(round + playerId) % spawnPointCount`. Everyone
moves up one spawn point per round, **together**, so the whole arrangement just
rotates — the gap between you and any other player never changes. If someone
starts one spawn over from you in round 1, they are one spawn over in round 50.

Measured over 300 rounds on a four-point map, how often a given pair of players
changes between sharing and not sharing a spawn point:

| Players | Vanilla | Spawn Shuffle |
|---|---|---|
| 5  | 0.0% | 17.8% |
| 8  | 0.0% | 24.2% |
| 10 | 0.0% | 28.9% |

Vanilla is not "rarely". It is *never*.

## What it does

Every round, players are dealt into spawn points through a fresh shuffle, and
the spawn points themselves are shuffled too. So who you start near, and which
part of the map gets used, both change round to round.

With four players or fewer everyone still gets their own spawn point, exactly
like vanilla — only the arrangement varies. Above four, players have to share
(maps only ship four spawn points), but every point stays occupied and the load
stays even: never three crowded onto one spawn while another sits empty.

## Installing

Drop `SpawnShuffle.dll` into `BepInEx/plugins/`, or install the zip through a
mod manager.

**Everyone in the lobby should have it.** Spawning is decided by the host, so a
host without it means nobody gets shuffled spawns.

## Works alongside

Nothing else is required, but it fits in with the usual 5-10 player setup:

- **moreStrafts** — no interaction; this patches vanilla spawning directly.
- **MoreStrafts_UISpawnAddon** — detected automatically. That mod already nudges
  every spawn apart, so this one stops doing its own nudging and just decides
  who goes where. No configuration needed.

## Settings

`BepInEx/config/com.caseypetrosky.spawnshuffle.cfg`

| Setting | Default | What it does |
|---|---|---|
| `Enabled` | `true` | Off restores vanilla spawns |
| `MinimumPlayers` | `3` | Smaller matches are left alone |
| `OverrideTeamModes` | `false` | Team modes keep the game's own 2v2 placement |
| `ClusterRadius` | `0.6` | Metres apart when players share a point |
| `SharedPointOffsets` | `Auto` | Leave separation to UISpawnAddon when installed |
| `Salt` | `0` | Change for a different sequence of shuffles |

## Is it working?

Press **F10** in a match. The line that matters:

```
spawns re-dealt so far  : 12
```

If that stays at `0`, it is not doing anything, and the next line says why —
too few players, team mode, and so on.

## If something goes wrong

Every failure falls back to vanilla spawning rather than breaking your match.
If you hit a problem, `BepInEx/LogOutput.log` plus the F10 report is everything
needed to diagnose it.

Bugs: https://github.com/caseypetrosky/Strafttat-Modding/issues
