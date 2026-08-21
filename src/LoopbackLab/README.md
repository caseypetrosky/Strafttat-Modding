# Loopback Lab

Run several STRAFTAT instances on one PC, one Steam account, so 5–10 player
lobby work can actually be tested. Development tool only — **not** something to
ship to players.

## Why it's needed

STRAFTAT's netcode is FishySteamworks, where a network address *is* a Steam ID:
hosting publishes `SteamUser.GetSteamID()` as the lobby's `HostAddress`, and
joining dials that Steam ID. Two instances on one account share one Steam ID, so
they cannot address each other. The blocker is the transport, not game logic.

The way out ships with the game: FishNet's **Tugboat** transport (plain TCP/UDP,
happy on `127.0.0.1`) is compiled into `FishNet.Runtime.dll`. STRAFTAT also
already has a `nonSteamworksTransport` flag it checks in movement, pause, HUD,
health and spawn code — the developers run the game this way themselves.

## Setup

1. Build (see the repo README), which drops `LoopbackLab.dll` into
   `BepInEx/plugins/LoopbackLab/`.
2. Launch once to generate `BepInEx/config/com.caseypetrosky.loopbacklab.cfg`.
3. Set `Enabled = true` in that file.
4. **Restart the game.** This is not optional — see "Restart required" below.
5. Launch a second instance by running `STRAFTAT.exe` directly from the install
   folder (not the Steam Play button). The game ships `steam_appid.txt`, so the
   Steam API still initialises in a directly-launched process.

## Use

| Key | Action |
|-----|--------|
| F9  | Host — starts a server and connects this instance's own client |
| F10 | Join — connects to `ClientAddress` (default `127.0.0.1`) |
| F11 | Stop — disconnects the client, and the server if hosting |

Press F9 in the first instance, F10 in the second. Watch the BepInEx console:
every action logs what happened and why if it didn't.

Config keys: `Port` (default 7770, all instances must agree), `MaximumClients`
(default 16), `ClientAddress`, and the three hotkeys.

## Restart required, and why

FishNet picks its transport exactly once. In 3.10.8,
`TransportManager.InitializeOnce_Internal` calls `Transport.Initialize(...)` and
caches per-channel MTUs from whatever transport is assigned at that instant —
and right after, `ClientManager` and `ServerManager` subscribe their handlers to
*that specific transport instance's* events.

Swapping the transport later fails in a nasty, silent way: connections look like
they start, but the managers are still listening to the old transport, so no
data is ever processed. So the swap runs as a **Harmony prefix** on that
initialisation method, before any binding happens — which means it can only take
effect at startup. Hence: change the config, restart.

This is also why `Enabled` defaults to `false`. With it off, the patch is still
applied but does nothing, so the plugin is safe to leave installed while playing
online normally.

## Design notes

- **No `Assembly-CSharp` reference.** Everything game-specific goes through
  Harmony's `AccessTools` by name (see `SceneMotorSpawner`), so the plugin
  depends only on FishNet, BepInEx and Unity. A game update that renames things
  produces a clear log message instead of a plugin that won't load.
- **Manager-level connection calls.** `ServerManager.StartConnection()` /
  `ClientManager.StartConnection(address)` rather than driving the transport
  directly, so FishNet's own state checks and connection-state events fire
  normally. (The game itself calls `StartConnection` straight on its transport;
  the manager calls are the supported path.)
- **SceneMotor.** Normal hosting instantiates a `SceneMotor` prefab and
  `ServerManager.Spawn`s it. Nothing else spawns it, and other systems read
  `SceneMotor.Instance`, so `Host()` reproduces that step.
- **Failure is always non-fatal.** A failed transport swap leaves the game on
  Steam; a failed SceneMotor spawn still leaves a connected session useful for
  UI testing; the hotkey handler is wrapped in try/catch.

## Status and open questions

Compile-verified against a FishNet 3.10.8 reference assembly built from
upstream source — the exact version the game ships. **Not yet run in-game**;
these are the things a first session should check:

- Does the game's own lobby UI cope with a session that has no Steam lobby
  behind it, or does hosting need to start from a different entry point?
- Both instances report the same Steam ID, so UI keyed on it (names, avatars,
  "is this my row") may duplicate or mis-match. Expected to be cosmetic.
- Is the leftover FishySteamworks component (left enabled, just not assigned)
  quiet in the background?

Fallbacks if Tugboat proves unworkable: a second Steam account in another
Windows user session keeps FishySteamworks intact, and most 5–10 player logic
only needs *player count*, not distinct humans.
