# MoreStrafts Recon

A read-only BepInEx plugin for STRAFTAT. It patches nothing and changes no game
behaviour — it just dumps information so you can find out how the lobby actually
works before you try to change it.

Three hotkeys:

| Key | What it does |
|-----|--------------|
| F6  | Dump the full GameObject tree of every loaded scene |
| F7  | Scan the game assembly for members matching keywords (player, max, count, team, ...) |
| F8  | Dump every live field value of a named type |

Output lands in `BepInEx/recon-dumps/`.

---

## Setup

One-time setup (SDK, BepInEx, console logging, `GameDir`) is in the
[repo README](../../README.md). Once that's done:

```bash
dotnet build src/MoreStraftsRecon
```

The DLL auto-copies into `BepInEx/plugins/MoreStraftsRecon/`. That's your whole
iteration loop: `dotnet build`, relaunch game, test.

**Launch.** You should see this in the console:

```
[Info   :MoreStrafts Recon] MoreStrafts Recon v0.1.0 loaded.
```

If you see that, everything works and you're a mod developer now.

---

## First recon session

Do these in order and keep the output files:

1. **Main menu** → press **F7**. Read `scan_*.txt`. You're looking for anything
   that smells like a player cap: `maxPlayers`, `MAX_PLAYERS`, `playerCount`,
   or an array/list with a hardcoded length of 4.

2. Note the type names that come back. Set `Recon.InspectType` in
   `BepInEx/config/com.caseypetrosky.morestraftsrecon.cfg` to the most promising one
   (a manager or lobby class), relaunch, press **F8**.

3. **Host a lobby, then press F6.** Open `hierarchy_*.txt` and find the player
   slot UI. The question that matters: are the four slots **siblings under one
   parent with identical structure**? If yes, they're clonable, and building a
   grid layout for 10 is very achievable. If they're hand-placed with different
   names and structures, the UI work is much harder.

4. **F8 again while in the lobby**, with a different type this time. Compare
   against your main-menu dump. Fields that changed are lobby state.

That's your evidence base. It's also the answer to the fork-vs-addon question we
kept deferring.

---

## Cross-reference

You have the real source at
https://github.com/Lemaitre-Logiciels/STRAFTAT-Public — grep it for the type
names your scan turns up. The dumps tell you what's live at runtime; the source
tells you why. Use both.

Read-and-learn only: that repo has no LICENSE file, so don't paste its code into
anything you distribute.

---

## Notes

- Much of what a first session was meant to discover is already written up in
  [docs/RECON-FINDINGS.md](../../docs/RECON-FINDINGS.md), with `file:line`
  references into the game source. Use the dumps to confirm it at runtime and to
  go past it, not to rediscover it.
- If `dotnet build` can't find `BepInEx.Core`, check `NuGet.config` at the repo
  root — BepInEx isn't on nuget.org.
- If you get missing-assembly errors, look in your `STRAFTAT_Data/Managed/`
  folder and adjust the `<Reference>` entries. Not every Unity build ships the
  same module DLLs. This code has been compile-checked against BepInEx 5.4.23.5
  and Unity 2021.3 references, so a failure here is most likely a path problem.
- If `netstandard2.1` gives you grief, change `TargetFramework` in
  `Directory.Build.props` to `net472`.
