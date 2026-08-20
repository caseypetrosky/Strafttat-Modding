# Solo multi-client testing — investigation

Goal: run 2+ STRAFTAT clients on one machine, one Steam account, so 5–10 player
lobby work can be tested without rounding up a group. Evidence below is from
STRAFTAT-Public source (file:line refs are into `STRAFTAT/Assets/Scripts/`).

## Why plain dual-instancing doesn't work

The shipping netcode is **FishySteamworks**: network addresses *are* Steam IDs.
Hosting sets the lobby's `HostAddress` to `SteamUser.GetSteamID()` and starts
the connection with the host's own Steam ID as client address
(`SYSTEM/SteamLobby.cs:448,464-465`); joining reads `HostAddress` from Steam
lobby data and connects to that Steam ID (`SteamLobby.cs:563-565`). Two
instances under one account share one Steam ID, so they cannot address each
other — the transport, not the game logic, is the blocker.

## The opening: the game ships a second transport

Two findings make a loopback path realistic:

1. **Tugboat is compiled into the game.** The FishNet import includes
   `Assets/FishNet/Runtime/Transporting/Transports/Tugboat/` under the
   `FishNet.Runtime` asmdef — meaning `FishNet.Runtime.dll` in
   `STRAFTAT_Data/Managed/` contains `FishNet.Transporting.Tugboat`, a plain
   TCP/UDP transport that happily does `127.0.0.1`. Nothing needs to be added
   to the game; the transport is already on disk.

2. **The game already handles a non-Steam transport.** `ClientInstance` sets
   `nonSteamworksTransport = (TransportManager.Transport != FishySteamworks)`
   (`SYSTEM/ClientInstance.cs:311`), and that flag is respected all over:
   spawn-point selection short-circuits to 1v1 spawns
   (`GAMEPLAY/PlayerManager.cs`, `SetActiveSpawnPoints`), movement is force-enabled
   (`CONTROLLER/FirstPersonController.cs:666`), pause and HUD behave differently
   (`SYSTEM/PauseManager.cs:88,130,263,355`), health logic checks it
   (`CONTROLLER/PlayerHealth.cs:117`), and there is even a debug self-hurt
   hotkey gated on it (`ClientInstance.cs:321`). The devs evidently run the
   game on a non-Steam transport during development — this is a supported-ish
   code path, not a hack we invented.

## What a "LoopbackLab" plugin has to do

A small BepInEx plugin (separate from the read-only recon plugin — this one
mutates state on purpose) with two hotkeys:

**Host (instance A):**
1. Add a `Tugboat` component next to the `NetworkManager`, set its port
   (e.g. 7770), and assign it as `InstanceFinder.TransportManager.Transport`.
2. Start the server (`ServerManager.StartConnection()`) and the local client
   (`ClientManager.StartConnection()`), replacing the FishySteamworks calls at
   `SteamLobby.cs:464-465`.
3. Spawn the scene-motor: vanilla hosting instantiates `_sceneMotorPrefab` and
   `ServerManager.Spawn`s it (`SteamLobby.cs:470-471`). The prefab is a private
   field on `SteamLobby` — grab it with
   `AccessTools.FieldRefAccess` (HarmonyX) and repeat those two lines.
4. Skip every `SteamMatchmaking.*` call — that's presentation (lobby name,
   browser visibility), not connectivity.

**Join (instance B, same machine):**
1. Same transport swap.
2. `ClientManager.StartConnection("127.0.0.1")` — the Tugboat equivalent of
   `SteamLobby.cs:563-565`, no Steam lobby data needed.

Second instance launch: run `STRAFTAT.exe` directly from the install folder
(don't use the Steam "Play" button twice). The repo ships `steam_appid.txt`,
so `SteamAPI_Init` succeeds in a directly-launched process; Steam features
keep working in both instances, we only bypass matchmaking.

## Known risks / open questions (first run answers these)

- **Transport swap timing.** FishNet wires the transport in
  `TransportManager.Awake`. Swapping after that generally works if done while
  fully disconnected, but this is *the* thing to verify first. If the property
  swap misbehaves, plan B is patching `TransportManager` during startup so
  Tugboat is selected before anything connects.
- **Duplicate Steam IDs.** Both instances report the same
  `SteamUser.GetSteamID()`. FishNet's own player identity (`PlayerId`,
  connection ids) is transport-level and will be distinct, but UI keyed on
  Steam IDs (`PlayerListItem.localSteamId`, names, avatars) may show the same
  name twice or mis-flag "own row". Cosmetic, but worth watching.
- **Lobby-UI entanglement.** How much of `SteamLobby`'s lobby screen assumes a
  real Steam lobby exists (`CurrentLobbyID`, member lists) — the loopback path
  may need to force `LobbyWindow` state or start straight from
  `AutomaticStart()`-style flow (`SteamLobby.cs:474`).
- **Steam ToS caveat.** Running extra direct-launched instances of a game you
  own, on your own machine, for local testing is standard modder practice, but
  it is not an officially supported Steam flow. Keep it offline/loopback.

## Fallbacks if Tugboat fights back

- **Second account on the same PC:** a family-shared or alt account in a
  second Windows user session (or Sandboxie) gives two distinct Steam IDs and
  keeps FishySteamworks — heavier setup, but zero code.
- **Recruit-one-friend testing:** most of the 5–10 logic (arrays, grid layout,
  round-end screen) only needs *player count*, not *distinct humans* — one
  friend plus loopback instances multiplies quickly.

## Why this matters for the fork decision

If loopback works, every fixed-array and UI change in the unified fork becomes
testable at 5+ players in minutes on one machine. That removes the last
practical objection to doing the UI rewrite properly instead of shipping
guesses. Priority: build LoopbackLab before the fork, not after.
