using Il2CppSG.Claymore.Movement.Dash;
using UntilYouFallArchipelago;

internal static class TimedTrapTests
{
    public static void Run()
    {
        TestEffectLength();
        var dodge = new NoBlockingTrapState();
        var exhaustion = new TimedTrapState();
        Assert(!exhaustion.Activate(true), "hub receipt is void");
        dodge.LoadRoom(true);
        exhaustion.LoadRoom(true);
        Assert(!exhaustion.Activate(false), "loading receipt is void locally");
        dodge.TryActivate(true, Array.Empty<(IntPtr, float)>());
        exhaustion.Activate(true);
        dodge.Advance(10, false);
        exhaustion.Advance(20, false);
        exhaustion.Activate(true);
        Assert(
            dodge.RemainingSeconds == 50 && exhaustion.RemainingSeconds == 60,
            "reset is independent and never adds time"
        );
        exhaustion.Advance(20, true);
        Assert(exhaustion.RemainingSeconds == 60, "menu/loading pauses time");
        exhaustion.ClearRoom();
        exhaustion.Advance(90, false);
        exhaustion.Activate(true);
        exhaustion.Advance(90, false);
        Assert(exhaustion.RemainingSeconds == 60, "clear pauses through reward and receipt until next room");
        exhaustion.LoadRoom(true);
        exhaustion.Advance(30, false);
        Assert(exhaustion.RemainingSeconds == 30, "next combat resumes without resetting");
        Assert(
            TimedTrapHudLayout.Height(0) == 0.27f && TimedTrapHudLayout.Height(1) > TimedTrapHudLayout.Height(0) + 0.1f,
            "rows preserve accepted baseline and separate rendered 64px panels"
        );
        dodge.Advance(60, false);
        Assert(
            !dodge.Active && exhaustion.Active && TimedTrapHudLayout.ExhaustionRow(dodge.Active) == 0,
            "one expiry compacts the surviving row without ending it"
        );
        Assert(TimedTrapHudLayout.ExhaustionRow(true) == 1, "Exhaustion stacks above Dodge");
        exhaustion.Advance(100, false);
        Assert(!exhaustion.Active && exhaustion.RemainingSeconds == 0, "expiry clamps to zero");
        exhaustion.Activate(true);
        exhaustion.EndRun();
        Assert(!exhaustion.Active && !exhaustion.Activate(true), "death/end run clears and voids receipts");
        exhaustion.LoadRoom(false);
        Assert(!exhaustion.Activate(true), "Runeforge never queues an effect");

        var dash = new PlayerDash();
        dash._dashChargesVariable.Value = 3;
        dash._dashChargesVariable.Max.Value = 3;
        dash._elapsedDashChargeRestoreTime = 0.9f;
        var recharge = new ExhaustionDashRecharge();
        Assert(
            recharge.Apply(dash) && dash._dashChargeRestoreTimeVariable.Value == 12f,
            "Exhaustion triples the live interval"
        );
        Assert(
            dash._dashChargesVariable.Value == 3
                && dash._dashChargesVariable.Max.Value == 3
                && dash._elapsedDashChargeRestoreTime == 0.9f,
            "charges, maximum and partial recharge survive receipt"
        );
        for (var frame = 0; frame < 1000; frame++)
            Assert(!recharge.Apply(dash), "repeated receipt/update does not compound");
        Assert(dash._dashChargeRestoreTimeVariable.Writes == 1, "unchanged frames never write native stats");
        dash._dashChargeRestoreTimeVariable.Value -= 0.8f;
        recharge.Apply(dash);
        Assert(
            Math.Abs(dash._dashChargeRestoreTimeVariable.Value - 9.6f) < 0.00001f,
            "AP recharge bonuses received during Exhaustion are also multiplied by three"
        );
        var bonusWrites = dash._dashChargeRestoreTimeVariable.Writes;
        for (var frame = 0; frame < 1000; frame++)
            Assert(!recharge.Apply(dash), "fractional AP interval remains stable across frames");
        Assert(dash._dashChargeRestoreTimeVariable.Writes == bonusWrites, "fractional interval never spams events");
        recharge.Clear();
        Assert(
            Math.Abs(dash._dashChargeRestoreTimeVariable.Value - 3.2f) < 0.00001f,
            "expiry removes only the trap contribution"
        );
        var writes = dash._dashChargeRestoreTimeVariable.Writes;
        recharge.Clear();
        Assert(dash._dashChargeRestoreTimeVariable.Writes == writes, "cleanup is idempotent");
        recharge.Apply(dash);
        var nextDash = new PlayerDash();
        recharge.Apply(nextDash);
        Assert(
            Math.Abs(dash._dashChargeRestoreTimeVariable.Value - 3.2f) < 0.00001f
                && nextDash._dashChargeRestoreTimeVariable.Value == 12f,
            "scene owner change restores old variable"
        );
        recharge.Clear();
        Assert(nextDash._dashChargeRestoreTimeVariable.Value == 4f, "run end restores current owner");
        Console.WriteLine("PASS: independent timed traps, HUD compaction and Exhaustion recharge penalty");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private static void TestEffectLength()
    {
        Assert(
            DeathLinkRules.Read(new Dictionary<string, object> { ["death_link"] = false, ["death_link_mode"] = "hard" })
                == DeathLinkMode.Off,
            "disabled death link ignores mode"
        );
        Assert(
            DeathLinkRules.Read(new Dictionary<string, object> { ["death_link"] = true, ["death_link_mode"] = "soft" })
                == DeathLinkMode.Soft,
            "soft mode"
        );
        try
        {
            DeathLinkRules.Read(new Dictionary<string, object> { ["death_link"] = true, ["death_link_mode"] = "x" });
            throw new Exception("bad mode accepted");
        }
        catch (InvalidDataException) { }
        var client = new ArchipelagoClient("unused-test-state", _ => { });
        Assert(client.TrapEffectLengthSeconds == 60, "local command defaults to sixty before login");
        foreach (var duration in new[] { 30, 31, 60, 90, 300 })
        {
            Assert(
                TimedTrapState.ReadDuration(new Dictionary<string, object> { ["trap_effect_length"] = (long)duration })
                    == duration,
                "slot duration accepts whole seconds"
            );
            var exhaustion = new TimedTrapState();
            var dodge = new NoBlockingTrapState();
            exhaustion.LoadRoom(true);
            dodge.LoadRoom(true);
            Assert(
                exhaustion.Activate(true, duration)
                    && dodge.TryActivate(true, Array.Empty<(IntPtr, float)>(), duration),
                "both effects use slot length"
            );
            Assert(
                exhaustion.DurationSeconds == duration
                    && dodge.DurationSeconds == duration
                    && exhaustion.RemainingSeconds == duration
                    && dodge.RemainingSeconds == duration,
                "receipt captures independent duration and remaining time for both effects"
            );
            exhaustion.Advance(duration / 2.0, false);
            Assert(
                exhaustion.RemainingSeconds / exhaustion.DurationSeconds == 0.5,
                "HUD fraction uses captured length, not sixty"
            );
            exhaustion.ClearRoom();
            exhaustion.Advance(1000, false);
            exhaustion.LoadRoom(true);
            Assert(exhaustion.RemainingSeconds == duration / 2.0, "custom length preserves room pause/transition");
            dodge.Advance(duration - 1, false);
            dodge.TryActivate(true, Array.Empty<(IntPtr, float)>(), duration);
            Assert(dodge.RemainingSeconds == duration, "duplicate resets to configured length without addition");
            exhaustion.Advance(duration, false);
            Assert(!exhaustion.Active && dodge.Active, "custom expiry does not affect the other timer");
            Assert(
                dodge.DurationSeconds == duration,
                "another effect's receipt or expiry cannot change captured length"
            );
            dodge.TryActivate(true, Array.Empty<(IntPtr, float)>(), 300);
            Assert(
                dodge.DurationSeconds == 300 && dodge.RemainingSeconds == 300,
                "only a new receipt changes captured length after a slot configuration change"
            );
        }
        foreach (var raw in new object?[] { null, 0, 29, 301, "bad", 30.5, double.NaN, long.MaxValue })
        {
            var slot = new Dictionary<string, object>();
            if (raw != null)
                slot["trap_effect_length"] = raw;
            try
            {
                TimedTrapState.ReadDuration(slot);
                throw new Exception("invalid slot duration accepted");
            }
            catch (InvalidDataException) { }
        }
        var state = new TimedTrapState();
        state.LoadRoom(true);
        state.Activate(true, 90);
        try
        {
            state.Activate(true, 301);
            throw new Exception("invalid activation length accepted");
        }
        catch (ArgumentOutOfRangeException)
        {
            Assert(
                state.DurationSeconds == 90 && state.RemainingSeconds == 90,
                "invalid duration cannot corrupt an active timer"
            );
        }
    }
}
