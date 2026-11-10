using UntilYouFallArchipelago;

internal static class EnemyStasisStateTests
{
    public static void Run()
    {
        var state = new EnemyStasisState();
        Check(!state.Receive(true), "hub receipt is void");
        state.LoadRoom(true);
        Check(state.Receive(true) && state.RemainingSeconds == 10, "combat starts fixed ten seconds");
        state.Advance(3, false);
        Check(state.RemainingSeconds == 7, "countdown");
        state.Advance(4, true);
        Check(state.RemainingSeconds == 7, "menu pause");
        state.ClearRoom();
        state.Advance(4, false);
        Check(state.RemainingSeconds == 7, "room clear pauses Stasis before rewards");
        state.Advance(4, true);
        Check(state.RemainingSeconds == 7, "menu/loading keep the between-room pause");
        Check(
            state.PauseBetweenRooms && new TimedTrapState().PauseBetweenRooms,
            "Stasis uses the shared between-room pause"
        );
        state.Advance(30, false);
        Check(state.Active && state.RemainingSeconds == 7, "reward selection does not consume remaining time");
        state.LoadRoom(true);
        Check(state.Active && state.RemainingSeconds == 7, "next combat preserves remaining time");
        state.Advance(2, true);
        Check(state.RemainingSeconds == 7, "next room loading pause");
        state.Advance(2, false);
        Check(state.RemainingSeconds == 5, "next ready combat resumes countdown");
        Check(state.Advance(5, false) && !state.Active, "expires only after combat time elapses");
        state.ClearRoom();
        Check(state.Receive(true) && state.Pending && !state.Active, "post-clear receipt queues instead of starting");
        Check(
            !state.Advance(30, false) && state.Pending && !state.Active && state.RemainingSeconds == 0,
            "pending HUD must not start or count down the gameplay effect during rewards"
        );
        Check(
            state.Receive(true) && state.Pending && !state.Active && state.RemainingSeconds == 0,
            "repeated reward receipt keeps one pending effect without starting the gameplay timer"
        );
        state.LoadRoom(true);
        Check(state.Pending && !state.Active, "load does not start before readiness");
        Check(!state.StartPending(false) && state.StartPending(true), "only ready combat starts pending");
        state.Advance(6, false);
        Check(state.Receive(true) && state.RemainingSeconds == 10, "repeat resets without stacking");
        state.LoadRoom(true);
        Check(state.RemainingSeconds == 10, "combat transition preserves active time");
        Check(state.Advance(10, false) && !state.Active, "expires");
        state.ClearRoom();
        state.Receive(true);
        state.LoadRoom(false);
        Check(!state.Pending && !state.Active && !state.Receive(true), "hub clears queued and active effect");
        state.LoadRoom(true);
        state.Receive(true);
        state.EndRun();
        Check(!state.Active && !state.Pending && !state.InRun, "death/end run cleanup");
        Check(
            TimedTrapHudLayout.StasisRow(false, false, false) == 0
                && TimedTrapHudLayout.StasisRow(true, false, false) == 1
                && TimedTrapHudLayout.StasisRow(false, true, false) == 1
                && TimedTrapHudLayout.StasisRow(true, true, false) == 2,
            "blue row compacts after red rows"
        );
        Check(
            SuperChargeRules.Amount(100, 1) == 100
                && SuperChargeRules.Amount(100, 2) == 100
                && SuperChargeRules.Amount(100, 0.5f) == 200,
            "native charge argument normalizes low multipliers"
        );
        Check(
            SuperChargeRules.Amount(100, 0) == null
                && SuperChargeRules.Amount(100, float.NaN) == null
                && SuperChargeRules.Amount(float.PositiveInfinity, 1) == null,
            "invalid charge math rejected"
        );
        Console.WriteLine("PASS: Enemy Stasis ten-second state, pending, pause, reset and run lifecycle");
    }

    private static void Check(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }
}
