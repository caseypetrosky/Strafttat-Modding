# STRAFTAT Modding

Working toward better 5–10 player lobby support in STRAFTAT (Unity 2021.3,
FishNet 3.10.8, BepInEx 5).

## Layout

```
src/
  Shared/             Helpers compiled into each plugin (no cross-plugin refs)
  MoreStraftsRecon/   Read-only recon plugin — dumps hierarchy, types, live values
  LoopbackLab/        Test harness — several game instances on one PC
  SpawnShuffle/       Re-deals spawn points each round so neighbours change
  StraftatCap/        Raises the lobby player cap above four (GPL-3.0)
tests/
  SpawnAssignmentTests/  Runs SpawnShuffle's deal logic off the game
  CapMathTests/          Runs StraftatCap's cap arithmetic off the game
docs/
  RECON-FINDINGS.md      What the lobby actually looks like, with source references
  SOLO-TESTING.md        Why one-account multi-instance is hard, and the way through
  SPAWN-SHUFFLE-TESTS.md What the spawn tests check, and the measured numbers
straftat DATA FROM GALE(Thunderstore Fork)/
                      Installed BepInEx profile: mod DLLs, configs, real log output
Directory.Build.props Shared build settings — GameDir lives here
NuGet.config          BepInEx feed (BepInEx.Core is not on nuget.org)
```

## Building

1. Install the [.NET SDK](https://dotnet.microsoft.com/download) (8.0+).
2. Install BepInEx 5.4.21+ into STRAFTAT (Thunderstore BepInExPack is easiest)
   and launch the game once so it creates its folders.
3. Turn the console on — in `BepInEx/config/BepInEx.cfg`:
   ```ini
   [Logging.Console]
   Enabled = true
   ```
   Without it you're debugging blind.
4. Point the build at your install: edit `GameDir` in `Directory.Build.props`
   (Steam → right-click STRAFTAT → Manage → Browse local files).
5. Build:
   ```bash
   dotnet build src/MoreStraftsRecon      # or src/LoopbackLab, src/SpawnShuffle
   ```

Each successful build copies its DLL into `BepInEx/plugins/<Name>/`. Build,
relaunch, test — that's the whole loop.

**Where the DLL lands** is worked out separately from `GameDir`, because it is
usually *not* under it: mod managers keep their own BepInEx install and inject
it at launch, so copying into the Steam folder would succeed while the plugin
never loads — with no error anywhere. A Gale profile is detected automatically
(`Default`, override with `-p:GaleProfile=Name`); otherwise the build falls back
to `GameDir/BepInEx/plugins`. Watch for this line:

```
--> MoreStraftsRecon.dll copied to .../BepInEx/plugins/MoreStraftsRecon
```

No such line means nothing was installed — pass the path explicitly with
`-p:PluginsDir=".../BepInEx/plugins"`.

`Directory.Build.props` holds everything shared: target framework, `GameDir`,
the BepInEx package reference, common Unity references, and the copy step. A new
plugin only declares what's unique to it — see `src/LoopbackLab/LoopbackLab.csproj`
for how short that ends up being.

You can override the game path per-build without editing anything:

```bash
dotnet build src/LoopbackLab -p:GameDir="D:/Games/STRAFTAT"
```

## The plugins

**MoreStraftsRecon** — patches nothing, changes nothing. F6 dumps the full scene
hierarchy, F7 scans the game assembly for members matching keywords, F8 dumps
live field values of a named type. Output lands in `BepInEx/recon-dumps/`.
See its README for a first-session script.

**LoopbackLab** — swaps FishNet onto its Tugboat transport so several instances
on one machine can play together, working around FishySteamworks using Steam IDs
as network addresses. Disabled by default; safe to leave installed.
See its README — especially the note on why it needs a restart.

**SpawnShuffle** — vanilla spawns everyone at
`spawnPoints[(round + playerId) % count]`, so the whole arrangement rotates in
lockstep and the gap between any two players never changes: measured over 300
rounds, a given pair's pairing changes 0.0% of the time. This re-deals players
into spawn points each round. Press F10 for a status report that works with one
player. Enabled by default; its decision logic is unit-tested off the game.

**StraftatCap** — raises the lobby cap above four. Vanilla derives the cap from a
dropdown whose three options are authored in the scene, so widening it is most of
the job; the catch is that FishySteamworks discards the transport cap at server
start, so it has to be re-applied afterwards. Press F9 in a lobby for a status
report that checks the whole chain without needing a second player. Capacity
only — the lobby UI, round-end and tab screens still assume four.

## Where things stand

Recon is done: `docs/RECON-FINDINGS.md` answers the four questions that were
blocking an architecture decision, with `file:line` references into the game
source. Short version — the lobby slots are clonable siblings, the player cap is
a soft `SteamLobby.maxPlayers` plus a handful of fixed `[4]` arrays and a
per-map spawn-point limit, and the recommended path is a unified GPL fork of
moreStrafts with the UI layer written fresh.

Next up is unblocking testing (LoopbackLab's first in-game run), then the fork
itself. SpawnShuffle is the first gameplay change: self-contained, works with or
without moreStrafts, and a useful trial run of the patching patterns a fork
would lean on.

## Shipping a mod to other people

```bash
dotnet build src/SpawnShuffle -p:Package=true -p:GameDir="<your STRAFTAT path>"
```

Produces `dist/SpawnShuffle-<version>.zip` in Thunderstore layout, which mod
managers also accept as a local import — hand that file to a friend and they can
install it without building anything.

Keep `<Version>` in the csproj and `version_number` in
`src/SpawnShuffle/packaging/manifest.json` in step; the build fails rather than
producing a zip whose manifest disagrees with the DLL inside it.

Only SpawnShuffle is packaged. MoreStraftsRecon and LoopbackLab are development
tools, and StraftatCap stands down whenever moreStrafts is present, so none of
them belong in someone else's game.

## Testing

```bash
cd tests/SpawnAssignmentTests && dotnet run   # exit 0 = all checks passed
cd tests/CapMathTests         && dotnet run
```

Logic that can be made independent of Unity is kept that way and tested here.
Anything touching the live game is marked in its README as compile-verified but
not yet run.

## Licensing

- **moreStrafts** is GPL-3.0. Anything forked from it stays GPL-3.0 with
  published source.
- **STRAFTAT-Public** has no LICENSE file, so all rights are reserved. Read it,
  learn from it, never paste it into anything distributable.
- **MoreStrafts_UISpawnAddon** has no public source and no stated license. It
  has been decompiled here only to understand *what* to build. None of its code
  may be reused.

Never mix these.
