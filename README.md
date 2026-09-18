# STRAFTAT Modding

Mods and tooling for [STRAFTAT](https://store.steampowered.com/app/2386720/STRAFTAT/)
(Unity 2021.3, FishNet 3.10.8, BepInEx 5).

## Straft Shuffle

The released mod. It stops you spawning next to the same player every round.

STRAFTAT picks your spawn with `(round + playerId) % spawnPointCount`, so
everyone advances one spawn point per round **together**. The arrangement only
rotates — the gap between you and any other player never changes. Someone who
starts one spawn over from you in round 1 is one spawn over in round 50.

Measured over 300 rounds on a four-point map, how often a given pair changes
between sharing and not sharing a spawn point:

| Players | Vanilla | Straft Shuffle |
|---|---|---|
| 5  | 0.0% | 17.8% |
| 8  | 0.0% | 24.2% |
| 10 | 0.0% | 28.9% |

Every round, players are dealt into spawn points through a fresh shuffle, and
the spawn points themselves are shuffled too. Four players or fewer still get
their own point each, exactly like vanilla; above that everyone shares evenly,
with no point left empty.

Only the host needs it — spawn placement runs in a server RPC — so mixed lobbies
are fine. Details and settings: [`src/StraftShuffle/`](src/StraftShuffle/README.md).

It works alongside **moreStrafts** and **MoreStrafts_UISpawnAddon**, detecting
the latter and leaving spawn separation to it rather than doubling up.

## Also here

Development tools, kept because the findings behind Straft Shuffle came out of
them. None are needed to play.

| | |
|---|---|
| [`src/StraftatCap`](src/StraftatCap/README.md) | Raises the lobby cap above four. Stands down when moreStrafts is installed. |
| [`src/MoreStraftsRecon`](src/MoreStraftsRecon/README.md) | Read-only: dumps the scene tree, scans types, inspects live fields. |
| [`src/LoopbackLab`](src/LoopbackLab/README.md) | Runs several game instances on one PC for testing. Unproven. |

## Notes on how the game works

Written up while figuring this out, with `file:line` references into the
[public source mirror](https://github.com/Lemaitre-Logiciels/STRAFTAT-Public):

- [Lobby internals](docs/RECON-FINDINGS.md) — what the four player slots
  actually are, where the cap lives, and which hardcoded `4`s are arrays versus
  individually named fields.
- [Multi-instance testing](docs/SOLO-TESTING.md) — why two clients on one Steam
  account cannot connect, and the transport that gets around it.
- [Spawn tests](docs/STRAFT-SHUFFLE-TESTS.md) — what the numbers above are
  measuring.

## Building

Needs the [.NET SDK](https://dotnet.microsoft.com/download) 8.0+ and a STRAFTAT
install (plugins reference the game's own assemblies, which are not
redistributable and so are not in this repository).

```bash
dotnet build src/StraftShuffle -p:GameDir="C:/Path/To/STRAFTAT"
```

The DLL is copied straight into `BepInEx/plugins/`. A Gale profile is detected
automatically; otherwise it falls back to the game folder. To produce a
distributable zip:

```bash
dotnet build src/StraftShuffle -p:Package=true -p:GameDir="C:/Path/To/STRAFTAT"
```

Logic that can be separated from Unity is, and is tested without the game:

```bash
cd tests/SpawnAssignmentTests && dotnet run   # exit 0 = passed
cd tests/CapMathTests         && dotnet run
```

## Licence

GPL-3.0-or-later — see [LICENSE](LICENSE).

## Credits and boundaries

- **moreStrafts** by Nitrogenia (GPL-3.0) — raises the lobby cap; studied while
  writing `StraftatCap`.
- **MoreStrafts_UISpawnAddon** by Yeastmans — rebuilds the 5-10 player lobby UI.
  No public source and no stated licence, so nothing of it is reused here; it is
  only detected at runtime so the two mods do not fight over spawn offsets.
- **STRAFTAT** by Lemaitre Bros. The public source mirror carries no licence
  file, so it is referenced and read, never copied.

No third-party binaries are redistributed here.
