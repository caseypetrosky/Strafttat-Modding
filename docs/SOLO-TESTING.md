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

## The swap must happen at startup, not on a hotkey

The obvious design — a hotkey that assigns `TransportManager.Transport` when you
want to test — **does not work**, and fails silently, which is worse. Reading
FishNet 3.10.8's own source (`Managing/Transporting/TransportManager.cs`,
`Managing/NetworkManager.cs`) shows why:

1. `TransportManager.InitializeOnce_Internal` calls `Transport.Initialize(...)`
   on whatever transport is assigned at that moment, and caches per-channel MTUs
   from it.
2. `NetworkManager` then initialises `ClientManager` (line 317) and
   `ServerManager` (line 318), and **each subscribes its handlers to that
   specific transport instance's events** — `OnClientReceivedData`,
   `OnServerConnectionState`, `OnRemoteConnectionState` and friends
   (`ServerManager.cs:442-458`, `ClientManager.cs:217-236`).

Those subscriptions are made once and never re-pointed. Assign a different
transport afterwards and the connection *appears* to start while the managers go
on listening to the old transport — no data is ever processed, and nothing logs
an error.

So the swap is a Harmony **prefix on `TransportManager.InitializeOnce_Internal`**,
which runs before any of that binding. Initialize and every subscription then
land on Tugboat naturally, with no re-subscription trickery and no reflection
into FishNet internals. The cost is that loopback mode is a startup decision:
config toggle plus restart, not a hotkey.

## What the LoopbackLab plugin does

A BepInEx plugin (separate from the read-only recon plugin — this one mutates
state on purpose), implemented in `src/LoopbackLab/`:

**At startup:** the prefix above adds a `Tugboat` component to the
`NetworkManager` GameObject, sets port / max clients / client address, and
assigns it — but only when `Loopback.Enabled` is true, so the plugin is inert
during normal online play.

**Host (instance A), on F9:**
1. `ServerManager.StartConnection()` then `ClientManager.StartConnection()`,
   replacing the FishySteamworks calls at `SteamLobby.cs:464-465`. These
   manager-level calls are the supported entry points; the game's own direct
   transport call is the unusual one.
2. Spawn the scene motor: vanilla hosting instantiates `_sceneMotorPrefab` and
   `ServerManager.Spawn`s it (`SteamLobby.cs:471-472`). Nothing else spawns it
   and other systems read `SceneMotor.Instance`, so it has to be reproduced —
   reached reflectively via `AccessTools`, since it's a private `[SerializeField]`.
3. Skip every `SteamMatchmaking.*` call — that's presentation (lobby name,
   browser visibility), not connectivity.

**Join (instance B, same machine), on F10:**
`ClientManager.StartConnection("127.0.0.1")` — the Tugboat equivalent of
`SteamLobby.cs:564-565`, no Steam lobby data needed.

Second instance launch: run `STRAFTAT.exe` directly from the install folder
(don't use the Steam "Play" button twice). The repo ships `steam_appid.txt`,
so `SteamAPI_Init` succeeds in a directly-launched process; Steam features
keep working in both instances, we only bypass matchmaking.

## Known risks / open questions (first run answers these)

- **Transport swap timing — resolved by reading FishNet's source**, see above.
  The prefix-on-initialisation approach is what the code demands; the remaining
  question is only whether the patch lands early enough in practice (BepInEx
  plugin `Awake` runs before scene load, so it should).
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
