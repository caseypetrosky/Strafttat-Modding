# Changelog

## \[0.1.0] - 2026-07-03 (Alpha Release)
### Changes
- Rebuilt for the latest STRAFTAT update, which had broken most of the mod's patches (lobby player list, max players, team dropdowns).
- Fixed Quick Load causing a stuck loading screen / errors at round end.
- Quick Load is now host-only and syncs correctly to all clients; its state no longer gets overwritten while browsing other lobbies.
- Round-end screen arrays extended so up to 10 player names/scores are tracked.
- Players without a lobby preview slot (5th+) no longer trigger errors when joining.
- Added optional per-instance debug logging (`Debug.PerInstanceLogFiles`, off by default) to `BepInEx/moreStrafts_debug/` for bug reports.

## \[0.0.5] - 2026-02-09 (Alpha Release)
### Changes
- Updates to README.md

## \[0.0.4] - 2026-02-09 (Alpha Release)
### Changes
- Added Quick Load toggle in lobby settings. Skips post-round screen when enabled. Fixes score text persisting on screen.
- Credit: @IsaacGHoward

## \[0.0.3] - 2025-10-05 (Alpha Release)

### Changes
- Updates to README.md

