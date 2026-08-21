# STRAFTAT Modding

Working toward better 5–10 player lobby support in STRAFTAT (Unity 2021.3,
FishNet 3.10.8, BepInEx 5).

## Layout

```
src/
  MoreStraftsRecon/   Read-only recon plugin — dumps hierarchy, types, live values
  LoopbackLab/        Test harness — several game instances on one PC
docs/
  RECON-FINDINGS.md   What the lobby actually looks like, with source references
  SOLO-TESTING.md     Why one-account multi-instance is hard, and the way through
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
   dotnet build src/MoreStraftsRecon      # or src/LoopbackLab
   ```

Each successful build copies its DLL into `BepInEx/plugins/<Name>/`. Build,
relaunch, test — that's the whole loop.

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

## Where things stand

Recon is done: `docs/RECON-FINDINGS.md` answers the four questions that were
blocking an architecture decision, with `file:line` references into the game
source. Short version — the lobby slots are clonable siblings, the player cap is
a soft `SteamLobby.maxPlayers` plus a handful of fixed `[4]` arrays and a
per-map spawn-point limit, and the recommended path is a unified GPL fork of
moreStrafts with the UI layer written fresh.

Next up is unblocking testing (LoopbackLab's first in-game run), then the fork
itself.

## Licensing

- **moreStrafts** is GPL-3.0. Anything forked from it stays GPL-3.0 with
  published source.
- **STRAFTAT-Public** has no LICENSE file, so all rights are reserved. Read it,
  learn from it, never paste it into anything distributable.
- **MoreStrafts_UISpawnAddon** has no public source and no stated license. It
  has been decompiled here only to understand *what* to build. None of its code
  may be reused.

Never mix these.
