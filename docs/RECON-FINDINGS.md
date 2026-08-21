# STRAFTAT Lobby Modding — Recon Findings

Date: 2026-08-20. Evidence sources:

- `moreStrafts` GitHub repo, `main` @ `a47d690` ("v0.1.0: modular rewrite") — GPL-3.0
- Installed Gale profile from Strafttat-Modding repo: `moreStrafts.dll` v0.1.0,
  `MoreStrafts_UISpawnAddon.dll` v2.1.0, BepInEx `LogOutput.log`
- Decompiled `MoreStrafts_UISpawnAddon.dll` (ILSpy, **read-to-learn only** — no
  public source, no license; nothing from it may be copied into distributable code)
- STRAFTAT-Public mirror @ `main` (sparse checkout of all 257 `.cs` files) —
  every claim below cross-checked against actual game source with file:line refs

Environment facts from the live log: BepInEx **5.4.23.5**, Unity **2021.3.45**,
Steam App ID 2386720, FishNet transport is **FishySteamworks**.

---

## The four known unknowns — answered

### 1. Are the lobby player slots clonable prefab-style siblings? — YES

The lobby UI is not four hand-crafted panels. It decomposes into three parts,
all driven by arrays on singleton controllers:

- **`LobbyController.previews`** — `AboubiPreviewLobby[]` (public field,
  `Assets/Scripts/SYSTEM/LobbyController.cs:29`), the 3D character preview
  pedestals. UISpawnAddon's `EnsurePreviewsExpanded` takes any existing live
  preview, calls `Object.Instantiate(previewGO, sameParent)`, and grows the
  array to 10. It works in practice, which is proof by experiment that the
  slots are identical siblings under a shared parent.
- **`LobbyController.clientPosition`** and **`.tabclientPosition`** —
  `Transform[]` position markers (`LobbyController.cs:24,27`) for the lobby
  list and tab screen. Vanilla places each row at
  `clientPosition[Clamp(PlayerId - 1, 0, len-1)].position`
  (`LobbyController.cs:128-130`) — overflow players would all pile onto the
  last marker, which is why the mods replace the placement logic.
- **`PlayerListItem`** — one instantiated row per player (spawned per join, so
  inherently N-player capable); it carries `PlayerIdNumber`, `PlayerSteamID`,
  and a `teamIdDropdown`.

Consequence: a 10-player grid is tractable. Clone one preview sibling, extend
the three arrays, recompute marker positions. No scene-file surgery needed.

### 2. Constants or fixed-length arrays? — BOTH, and the arrays are the real work

The cap itself is soft: **`public int maxPlayers = 4`** on `SteamLobby`
(`Assets/Scripts/SYSTEM/SteamLobby.cs:34`). Vanilla's own
`SetMaxPlayers(TMP_Dropdown)` (`SteamLobby.cs:257-265`) computes
`maxPlayers = dropdown.value + 2` (so the stock dropdown offers 2/3/4), then
sets the Steam lobby member limit and the transport's max clients —
`SetMaximumClients(maxPlayers - 1)` because the host isn't counted as a client
(the dev's own comment says so, right before a second direct call on
`_fishySteamworks` with the note "why does this not use fishnets transport
system"). moreStrafts only has to repopulate the dropdown 2–10 and re-run the
same calls with the bigger number — it's driving vanilla's own machinery, not
inventing a mechanism. It also clamps `maxPlayers` in the `UpdateOnClients`
RPC, which matters because vanilla **self-kicks** any player whose index is
past `maxPlayers` (`SteamLobby.cs:238-243`).

The hard 4s are fixed-length arrays threaded through UI and round flow:

| Array | Where | How it's dealt with today |
|---|---|---|
| `names = new string[4]`, `scores = new int[4]` (fields, re-allocated each round) | `RoundManager.NextRoundCall` (`RoundManager.cs:167-168`) | moreStrafts **transpiler** rewrites `ldc.i4.4; newarr` → `ldc.i4.s 10` (2 expected matches; logs a warning if a game update changes the pattern) |
| `LobbyController.previews` | scene-authored, length 4 | moreStrafts just suppresses preview RPCs for id ≥ 4; UISpawnAddon clones slots to 10 |
| `clientPosition` / `tabclientPosition` | scene-authored, length 4 | moreStrafts stacks overflow rows 0.5 units below the last marker (the "broken UI"); UISpawnAddon relayouts into a 2-row grid (5/0, 3/3, 4/3, 4/4, 5/4…) |
| `secondaryPointObjects`, `pointsTexts`, `activeMaterials` | `MatchPoitnsHUD` (game's typo) | both mods reimplement `UpdateVisuals` |

Also real: **spawn points are a map-authoring limit, not just code.** Maps
carry two spawn sets found by tag — `"Spawnpoints"` (1v1) and
`"Spawnpoints4Player"` (`Assets/Scripts/GAMEPLAY/PlayerManager.cs`,
`PopulateSpawnPoints`). With more than 2 players the game uses the 4-player
set; if a map has none it logs `"No spawn points found for 4+ players!
Falling back to 1v1 spawn points."` (`PlayerManager.cs:180`) — that exact
error appears in the live log. Nothing is authored for >4 anywhere, so 5–10
players reuse at most 4 spawn points. UISpawnAddon works around the resulting
same-point collisions by offsetting each spawn on a 0.5 m circle
(`playerId * 36°`) in a `PlayerManager.SpawnPlayer` prefix. Any serious 5–10
design inherits this per-map problem.

### 3. Does STRAFTAT-Public build? — No, and it doesn't need to

Confirmed directly: `STRAFTAT/Assets/FishNet/` exists in the mirror but
contains **zero `.cs` files** — only `.meta` skeletons survive. `sanitize.py`
lists `STRAFTAT\Assets\FishNet` (FishNet **v3.10.8R**, per its comment) among
the licensed directories whose non-meta files it deletes, and FishNet isn't in
`Packages/manifest.json` either (it's an Asset Store import, not a UPM
package). Without the netcode the project cannot compile. Treat the mirror as
a **greppable reference** for type names, field names, and logic — which is
exactly what recon needs. Runtime truth comes from the recon plugin against
the shipping game.

### 4. The v0.0.4 / v0.0.5 version mystery — solved, it's tag lag

GitHub `main` is the v0.1.0 modular rewrite; the maintainer simply never pushed
tags past v0.0.4. Thunderstore is the release channel of record: the installed
manifest says moreStrafts **0.1.0**, and UISpawnAddon's manifest depends on
`Nitrogenia-moreStrafts-0.0.5`. So "requires 0.0.5 or later" was written
against Thunderstore versions that GitHub tags never reflected. No missing
source: `main` @ `a47d690` corresponds to the shipping 0.1.0.

---

## What the two mods actually do (mechanics that matter)

**moreStrafts v0.1.0** — 16 Harmony patches: dropdown/transport/RPC-clamp for
the cap, the `NextRoundCall` transpiler, `PlayerListItem.Update` prefix
(returns `false`, i.e. wholesale replaces the method), preview-RPC suppression
for id ≥ 4, FFA-as-unique-teams (`ScoreManager.SetTeamId(playerId, playerId)`),
`MatchPoitnsHUD.UpdateVisuals` replacement, QuickLoad feature. `ModState.MaxPlayers = 10`
is a compile-time constant.

**UISpawnAddon v2.1.0** — on `Awake` it calls `Harmony.Unpatch(...,
"com.nitrogenia.morestrafts")` on **five** of moreStrafts' patches
(`PlayerListItem.Update`, three preview RPCs, `MatchPoitnsHUD.UpdateVisuals`)
and installs its own replacements: preview-slot cloning, 2-row grid layout,
tab-screen scores, extended victory screen (a large coroutine rebuilt around
`RoundManager.NextRoundCall`), circular spawn offsets.

**Fragility, concretely observed:**

- The addon depends on moreStrafts' *Harmony ID and exact patch set* to
  surgically remove them. Any moreStrafts release that renames/moves a patch
  half-breaks the pair — this is the patch-on-patch drift the task doc suspected.
- Both mods patch FishNet **codegen'd RPC bodies by hashed name**
  (`RpcLogic___UpdateOnClients_3316948804` etc.). Harmony/FishNet gotcha: those
  methods don't exist in anyone's source — FishNet's IL weaver generates them
  at build time, and the numeric suffix can change if the devs update FishNet
  (currently v3.10.8R) or touch RPC signatures — silently breaking patches.
- The game has its own mod detection: the log shows
  `"Incompatible assemblies found: moreStrafts…, MoreStrafts_UISpawnAddon…"`
  at startup and later broadcasts `"Player <name> has the following mods: …"`
  per lobby member. Modded clients are visibly flagged to peers — worth
  understanding how lobbies treat that before shipping anything.

---

## Recon tooling status

`NewStraftsRecon` (in the Strafttat-Modding repo) **compiles clean** —
0 errors / 0 warnings — as `netstandard2.1` against:

- BepInEx **5.4.23.5** DLLs taken from the Gale profile (the NuGet
  `BepInEx.Core 5.*` path also works where the BepInEx feed is reachable), and
- Unity 2021.3 reference assemblies (`UnityEngine.Modules` 2021.3.33 — the
  recon code only touches CoreModule + SceneManagement, so no TMP/uGUI needed).

The only unverified step is running it in-game (needs the real
`STRAFTAT_Data/Managed` on the Windows box). Recon priorities with it, in
order: F6 in-lobby (confirm preview-slot parent/sibling structure in the live
scene), F8 on `SteamLobby` and `LobbyController` (live `maxPlayers`, array
lengths), F7 for anything the mods *don't* patch that still assumes 4.

---

## Path recommendation (evidence-based)

**Unified fork of moreStrafts (GPL-3.0, published source), reimplementing the
UI layer from scratch.** Reasoning:

- moreStrafts' cap approach is *correct* (unknown 2 confirms: the cap is soft;
  its dropdown/transport/RPC-clamp/transpiler mechanics are the right shape) —
  so ground-up is unjustified.
- The addon path is structurally the worst option: UISpawnAddon exists by
  unpatching its dependency at runtime, has no public source and no license, so
  it can neither be fixed nor forked when it drifts. Every future STRAFTAT or
  moreStrafts update re-rolls the dice.
- A fork collapses the two-mod drift problem into one DLL. The UI work
  (slot cloning, grid layout, tab scores, victory screen) must be **written
  fresh** — the addon's decompilation told us *what* to build and that cloning
  works, but its code cannot be reused. moreStrafts' own GPL code can.
- Licensing shape: fork stays GPL-3.0 with published source; nothing from
  STRAFTAT-Public or the addon DLL is pasted in.

Solo-testability note: the lobby UI parts (slot cloning, grid relayout,
dropdown, round-end arrays) can be exercised with a hosted 1-player lobby plus
the recon dumps; genuinely multi-client behaviors (RPC clamp, spawn collisions,
per-player tab scores) still need a multi-client rig — that remains the top
unblocker before committing to implementation. See **SOLO-TESTING.md**: the
game ships FishNet's Tugboat transport and already handles
`nonSteamworksTransport` throughout, so a loopback test mod looks viable.
