using UntilYouFallArchipelago;

internal static class CursedTrapStateTests
{
    public static void Run()
    {
        var state = new CursedTrapState();
        Assert(state.ScaleDamage(2f) == 2f && !state.Activate(true), "hub cannot apply curse");
        state.LoadRoom(true);
        Assert(!state.Activate(false), "unready receipt rejected");
        Assert(state.Activate(true, 30), "combat receipt activates curse");
        Assert(state.ScaleDamage(1.5f) == 3f, "positive damage doubles");
        Assert(state.ScaleDamage(2f) * 1.5f == 6f, "native scalar still composes multiplicatively");
        Assert(state.ScaleDamage(0f) == 0f && state.ScaleDamage(-1f) == -1f, "non-damage unchanged");
        state.Advance(10, false);
        Assert(
            state.Activate(true, 30) && state.RemainingSeconds == 30 && state.ScaleDamage(2f) == 4f,
            "repeat receipt resets duration without multiplying strength"
        );
        state.ClearRoom();
        state.Advance(20, false);
        Assert(state.RemainingSeconds == 30, "room clear pauses curse");
        state.LoadRoom(true);
        state.Advance(20, true);
        Assert(state.RemainingSeconds == 30, "menu/loading pause preserves curse");
        Assert(state.Advance(30, false) && state.ScaleDamage(2f) == 2f, "expiry restores normal damage");
        state.Activate(true);
        state.LoadRoom(false);
        Assert(state.ScaleDamage(2f) == 2f, "hub transition clears curse");
        state.LoadRoom(true);
        state.Activate(true);
        state.EndRun();
        Assert(state.ScaleDamage(2f) == 2f && !state.InRun, "run end clears curse");
        foreach (var dodge in new[] { false, true })
        foreach (var exhaustion in new[] { false, true })
        foreach (var cursed in new[] { false, true })
        {
            var prior = (dodge ? 1 : 0) + (exhaustion ? 1 : 0);
            Assert(TimedTrapHudLayout.CursedRow(dodge, exhaustion) == prior, "curse row compacts");
            Assert(
                TimedTrapHudLayout.StasisRow(dodge, exhaustion, cursed) == prior + (cursed ? 1 : 0),
                "stasis stays above all active traps"
            );
        }
        Console.WriteLine("PASS: Cursed Trap damage, timer lifecycle and HUD stacking");
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok)
            throw new Exception("Cursed Trap: " + message);
    }
}
