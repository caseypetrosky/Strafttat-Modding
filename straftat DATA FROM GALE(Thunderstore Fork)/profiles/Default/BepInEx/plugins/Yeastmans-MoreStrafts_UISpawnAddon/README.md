# MoreStrafts UI and Spawn Fix Addon v2.0.0

A companion addon for the **moreStrafts** mod that makes 5-10 player lobbies fully functional with proper UI layout, player previews, scores, and spawn handling.

## Features

- **Dynamic Grid Layout** - Players 5-10 arranged in a clean two-row grid (not stacked/overlapping)
- **3D Lobby Previews** - All player models visible and correctly positioned for any player count
- **Per-Player Tab Scores** - Score displayed next to each player on the tab screen 
- **Extended End-Round Screen** - All players shown with names and scores after each round
- **Team Dropdown** - Expanded team selection with clean number labels for all players
- **Spawn Offsets** - Players spawn in a circle offset so no one blocks your view
- **Hat/Cosmetic Sync** - Hats and outfits mostly display correctly on all player previews

## Layout Grid

| Players | Row 1 | Row 2 |
|---------|-------|-------|
| 5       | 5     | -     |
| 6       | 3     | 3     |
| 7       | 4     | 3     |
| 8       | 4     | 4     |
| 9       | 5     | 4     |
| 10      | 5     | 5     |

## Installation (Mod Manager)
- Install with **Thunderstore Mod Manager** 
## Manual Installation
1. Install BepInExPack for STRAFTAT
2. Install the [moreStrafts](https://thunderstore.io/c/straftat/p/Nitrogenia/moreStrafts/) mod (v0.0.4+)
3. Copy `MoreStrafts_UISpawnAddon.dll` to `BepInEx/plugins/`

## Notes
- Requires moreStrafts v0.0.5 or later
- With 4 or fewer players, vanilla behavior is preserved

