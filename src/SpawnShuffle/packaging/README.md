# Spawn Shuffle

Stops you spawning next to the same player every round.

## Why

STRAFTAT picks your spawn with `(round + playerId) % spawnPoints`. Everyone
moves up one spawn point per round **together**, so the arrangement only
rotates — the gap between you and anyone else never changes. Whoever starts one
spawn over from you in round 1 is still one spawn over in round 50.

This re-deals everyone into spawn points each round, and shuffles the spawn
points too, so both who you start near and which part of the map gets used
change every round.

Four players or fewer still get a spawn point each, same as vanilla. Above that
people share — maps only have four — but the load stays even and no point is
left empty.

## Install

Install through a mod manager, or drop `SpawnShuffle.dll` into
`BepInEx/plugins/`.

**Only the host needs it.** Spawns are decided host-side, so mixed lobbies are
fine — anyone without it is unaffected.

## Requirements

- BepInEx 5

## Works with

- **moreStrafts** — no conflict.
- **MoreStrafts_UISpawnAddon** — detected automatically. It already spreads
  spawns apart, so this mod stops doing its own spreading and just decides who
  goes where. Nothing to configure.

## Settings

`BepInEx/config/spawnshuffle.cfg`

| Setting | Default | |
|---|---|---|
| `Enabled` | `true` | Off restores vanilla spawns |
| `MinimumPlayers` | `3` | Smaller matches are left alone |
| `OverrideTeamModes` | `false` | Team modes keep the game's own placement |
| `ClusterRadius` | `0.6` | Metres apart when players share a point |
| `SharedPointOffsets` | `Auto` | Defers to UISpawnAddon when installed |
| `Salt` | `0` | Change for a different sequence |

## Check it's working

Press **F10** on the host during a match:

```
spawns re-dealt so far  : 12
```

If that stays at `0`, the next line says why — usually too few players or a
team mode.

Anything that goes wrong falls back to normal spawning rather than breaking
your match.
