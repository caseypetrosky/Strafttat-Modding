using System;
using System.Linq;
using StraftatCap;
using StraftatModding;

class Program
{
    static int failures;
    static void Check(bool ok, string what)
    {
        if (!ok) { Console.WriteLine($"  FAIL: {what}"); failures++; }
    }

    static void Main()
    {
        // 1. The dropdown relationship must match vanilla exactly. Vanilla's own
        //    SetMaxPlayers still runs and recomputes value+2, so if our mapping
        //    disagreed, picking "10 Players" would quietly set something else.
        Console.WriteLine("1. dropdown index <-> player count matches vanilla (value + 2)");
        for (int index = 0; index <= CapMath.MaxPlayers - CapMath.MinPlayers; index++)
            Check(CapMath.PlayersForDropdownIndex(index) == index + 2, $"index {index} -> {index + 2}");
        Check(CapMath.PlayersForDropdownIndex(0) == 2, "index 0 is a 1v1");
        Check(CapMath.PlayersForDropdownIndex(8) == 10, "index 8 is a full ten-player lobby");

        // Round-tripping matters: we set the index from a count when rebuilding
        // the dropdown, and vanilla reads the count back from the index.
        Console.WriteLine("2. index/count round-trips");
        for (int players = CapMath.MinPlayers; players <= CapMath.MaxPlayers; players++)
            Check(CapMath.PlayersForDropdownIndex(CapMath.DropdownIndexForPlayers(players)) == players,
                  $"{players} survives a round trip");

        // 3. One option per selectable count, labelled with that count.
        Console.WriteLine("3. dropdown options");
        var options = CapMath.DropdownOptions(CapMath.MaxPlayers);
        Check(options.Count == CapMath.MaxPlayers - CapMath.MinPlayers + 1,
              $"ten-player cap offers 9 options, got {options.Count}");
        Check(options.First() == "2 Players", "first option is 2 Players");
        Check(options.Last() == "10 Players", "last option is 10 Players");
        for (int i = 0; i < options.Count; i++)
            Check(options[i] == $"{CapMath.PlayersForDropdownIndex(i)} Players",
                  $"option {i} names the count vanilla will derive from it");

        // 4. Transport allowance excludes the host.
        Console.WriteLine("4. transport clients excludes the host");
        Check(CapMath.TransportClientsFor(2) == 1, "1v1 needs one client slot");
        Check(CapMath.TransportClientsFor(10) == 9, "ten players need nine client slots");
        for (int players = CapMath.MinPlayers; players <= CapMath.MaxPlayers; players++)
            Check(CapMath.TransportClientsFor(players) == players - 1, $"{players} -> {players - 1}");

        // 5. Clamping, including nonsense input.
        Console.WriteLine("5. clamping");
        Check(CapMath.Clamp(0) == 2 && CapMath.Clamp(-5) == 2, "below range clamps up to 2");
        Check(CapMath.Clamp(99) == 10, "above range clamps down to 10");
        Check(CapMath.Clamp(7) == 7, "in-range value is untouched");
        Check(CapMath.TransportClientsFor(99) == 9, "transport count clamps too");
        Check(CapMath.DropdownIndexForPlayers(99) == 8, "index clamps too");

        // 6. The self-kick rule, which is what actually drops players out of
        //    oversized lobbies when a client's cap is stale.
        Console.WriteLine("6. self-kick boundary");
        Check(!CapMath.WouldSelfKick(3, 4), "player 4 of 4 stays");
        Check(CapMath.WouldSelfKick(4, 4), "player 5 of 4 kicks itself");
        Check(!CapMath.WouldSelfKick(9, 10), "player 10 of 10 stays");
        Check(CapMath.WouldSelfKick(4, 4) && !CapMath.WouldSelfKick(4, 10),
              "the same player survives once the cap actually reaches them");

        // 7. Vanilla drops a cap update that is smaller than the crowd present.
        Console.WriteLine("7. shrink-below-occupancy is ignored by vanilla");
        Check(CapMath.UpdateWouldApply(4, 10), "growing applies");
        Check(CapMath.UpdateWouldApply(4, 4), "equal applies");
        Check(!CapMath.UpdateWouldApply(6, 4), "shrinking below the crowd is ignored");

        // 8. StaticAccess must read a singleton however it is declared.
        //    This is a regression test for a shipped bug: every STRAFTAT
        //    singleton (SteamLobby, ScoreManager, GameManager) is a static
        //    FIELD, but the bridges asked AccessTools for a PROPERTY. That
        //    returns null, so the plugins decided the game had changed and
        //    stood down - compiling cleanly and logging a plausible warning
        //    while doing nothing at all.
        Console.WriteLine("8. static singletons resolve as field or property");
        Check(StaticAccess.Read(typeof(FieldSingleton), "Instance") is FieldSingleton,
              "reads a static FIELD (how STRAFTAT declares them)");
        Check(StaticAccess.Read(typeof(PropertySingleton), "Instance") is PropertySingleton,
              "reads a static PROPERTY");
        Check(StaticAccess.Getter(typeof(FieldSingleton), "Nope") == null,
              "missing member yields null rather than throwing");
        Check(StaticAccess.Getter(null, "Instance") == null, "null type is handled");
        Check(StaticAccess.Getter(typeof(FieldSingleton), "NotStatic") == null,
              "an instance member is not mistaken for a static one");

        Console.WriteLine(failures == 0 ? "\nALL PASS" : $"\n{failures} FAILURES");
        Environment.Exit(failures == 0 ? 0 : 1);
    }
}


// Stand-ins for the two ways a Unity singleton gets written.
class FieldSingleton
{
    public static FieldSingleton Instance = new FieldSingleton();
    public FieldSingleton NotStatic = null;
}

class PropertySingleton
{
    public static PropertySingleton Instance { get; } = new PropertySingleton();
}
