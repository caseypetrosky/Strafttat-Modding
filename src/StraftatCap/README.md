# Straftat Cap

Raises STRAFTAT's lobby player cap above four. **Capacity only** — this does not
touch the lobby UI, round-end screen or tab screen, all of which still assume
four players in ways a bigger number alone will not fix.

## What raising the cap actually involves

Vanilla derives the cap straight from a dropdown whose three options (2, 3, 4)
are authored in the scene:

```csharp
maxPlayers = _dropdown.value + 2;   // SteamLobby.SetMaxPlayers
```

So widening the dropdown is most of the work, and vanilla's own handler keeps
running unchanged. Three things beyond that need care.

### The transport throws your cap away at server start

This is the part that is easy to get wrong, and it fails silently.

FishySteamworks keeps its own serialised `_maximumClients`, which STRAFTAT ships
set to **3** — four players including the host. `SetMaximumClients` is a proper
override, but it only writes to the live server socket:

```csharp
public override void SetMaximumClients(int value) => _server.SetMaximumClients(value);
```

Starting the server then does this:

```csharp
_server.StartConnection(_serverBindAddress, _port, _maximumClients, _peerToPeer);
// ServerSocket.StartConnection -> SetMaximumClients(maximumClients)
```

It passes the **serialised** field, and the socket overwrites whatever it was
told earlier. Any cap applied before the server starts is discarded at start —
the Steam lobby advertises ten slots while the transport accepts three clients,
and nothing logs a complaint.

So the cap is re-applied **after** the server reports `Started`, via FishNet's
own `ServerManager.OnServerConnectionState`. No patching of FishySteamworks.

### Every client needs the cap, or it ejects itself

Vanilla's `Update` kicks any player whose index reaches their own `maxPlayers`:

```csharp
for (int i = maxPlayers; i < l; i++) { if (... == players[i] ...) KickSelf(); }
```

That runs on each client against **its own** copy. A client holding a stale or
smaller cap drops itself out of a lobby the host thinks is fine — which is why
everyone should run the same `MaxPlayers` value, and why the incoming cap is
clamped rather than trusted.

### Shrinking a full lobby does nothing

`UpdateOnClients` opens with `if (players.Count > maxPlayers) return;`, so a cap
lower than the crowd already present is dropped rather than applied. Not a bug to
fix, just behaviour worth knowing.

## Checking it works, alone

Press **F9** in a lobby. Every value the cap depends on is logged at once, with a
verdict:

```
---- cap report ----
configured ceiling      : 10
SteamLobby.maxPlayers   : 10
players present         : 1
dropdown options        : 9 (expected 9)
transport clients now   : 9 (expected 9)
transport agrees with the lobby (9 clients, 10 players).
--------------------
```

A `TRANSPORT MISMATCH` line means the transport would refuse players the lobby
advertises — the exact failure described above. No second player required to
see it.

## Config

`BepInEx/config/com.caseypetrosky.straftatcap.cfg`

| Setting | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Off restores vanilla's four |
| `MaxPlayers` | `10` | Largest lobby offered; everyone in a lobby should match |
| `Report` | `F9` | Log the status report |

## Design notes

No reference to `Assembly-CSharp` or TextMeshPro. Everything game-specific goes
through `GameBridge` using `AccessTools`, resolved once and cached, so a renamed
member degrades to a logged warning and vanilla behaviour instead of a plugin
that will not load. It also keeps the plugin buildable and verifiable without the
game's assemblies.

The cap RPC is found by **prefix** (`RpcLogic___UpdateOnClients*`) rather than by
its full hashed name. FishNet's IL weaver generates that suffix, so hardcoding it
breaks on any FishNet or signature change.

`CapMath` holds the arithmetic with no Unity types in it, and is unit-tested off
the game — see `tests/CapMathTests`.

## Status

Compile-verified, and the arithmetic is tested. **The in-game behaviour is not
yet confirmed**, and the transport finding above is reasoned from FishySteamworks
source rather than observed with real players. F9 is there precisely so the first
run answers that.

## Licence

GPL-3.0-or-later; see `LICENSE`. This plugin is an independent implementation,
but it covers the same ground as [moreStrafts](https://github.com/ALBINALSHAIKH/moreStrafts)
(GPL-3.0) by Nitrogenia, which was studied while writing it — so it is
distributed under the same licence, with thanks.

No code from `MoreStrafts_UISpawnAddon` was used; it has no public source and no
stated licence. Nothing from STRAFTAT-Public is reproduced here either.
