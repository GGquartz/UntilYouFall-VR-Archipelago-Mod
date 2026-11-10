using UntilYouFallArchipelago;

internal static class NoBlockingTrapStateTests
{
    public static void Run()
    {
        var state = new NoBlockingTrapState();
        var oldProjectile = new IntPtr(123);
        var newProjectile = new IntPtr(456);
        var existing = new[] { (oldProjectile, 10f) };
        Assert(!state.Active && !state.ShouldForceDodge(true, true), "starts inactive");
        Assert(
            !state.TryActivate(false, existing) && !state.Active,
            "hub, loading and missing living player reject without pending effect"
        );
        Assert(!state.TryActivate(true, existing), "readiness alone cannot activate outside a loaded run");
        state.LoadRoom(true);
        Assert(
            state.TryActivate(true, existing) && state.RemainingSeconds == 60,
            "first receipt starts sixty seconds in a living run, including a cleared room"
        );
        state.Advance(20, false);
        Assert(state.RemainingSeconds == 40, "active gameplay consumes real seconds");
        state.Advance(120, true);
        Assert(state.RemainingSeconds == 40, "reward selection, pause and loading consume no time");
        Assert(
            !state.TryActivate(false, existing) && state.RemainingSeconds == 40,
            "an ineligible receipt cannot refresh an already running timer"
        );
        Assert(state.ShouldForceDodge(true, true), "new attacks use native dodge while active");
        Assert(
            !state.ShouldForceDodge(false, true) && !state.ShouldForceDodge(true, false),
            "other players and invalid gameplay contexts are not affected"
        );
        Assert(
            !state.SuppressProjectileBlock(oldProjectile, 10f),
            "already flying projectiles retain their original block behavior"
        );
        Assert(
            state.SuppressProjectileBlock(newProjectile, 11f) && state.SuppressProjectileBlock(oldProjectile, 11f),
            "new projectiles and a new launch of the old instance cannot block"
        );
        Assert(
            state.TryActivate(true, new[] { (newProjectile, 11f) }) && state.RemainingSeconds == 60,
            "another receipt resets to sixty rather than adding sixty"
        );
        Assert(
            state.SuppressProjectileBlock(newProjectile, 11f) && !state.SuppressProjectileBlock(oldProjectile, 10f),
            "duplicate receipt does not replace the original activation boundary"
        );

        state.LoadRoom(true);
        Assert(
            state.Active && state.RemainingSeconds == 60,
            "next room preserves remaining time without replaying a receipt"
        );
        Assert(
            state.SuppressProjectileBlock(oldProjectile, 10f),
            "old-room projectile exemptions do not leak into another room"
        );
        state.Advance(59.5, false);
        Assert(state.Active && state.RemainingSeconds == 0.5, "fractional final second remains active");
        Assert(
            state.Advance(1, false) && !state.Active && state.RemainingSeconds == 0,
            "expiry clamps to zero and ends the effect exactly once"
        );
        Assert(!state.Advance(1, false), "expired timer does not end again");
        Assert(
            !state.ShouldForceDodge(true, true) && !state.SuppressProjectileBlock(newProjectile, 11f),
            "expiry restores normal melee and projectile decisions"
        );
        Assert(state.TryActivate(true, existing), "a fresh receipt can activate after expiry");
        Assert(state.EndStage() && !state.Active, "end run removes the effect");
        Assert(!state.EndStage(), "repeated end is a no-op");
        Assert(
            !state.Active && !state.SuppressProjectileBlock(oldProjectile, 11f),
            "end run removes projectile exceptions"
        );

        foreach (var reason in new[] { "death", "abandon", "ending", "player-destroyed", "shutdown" })
        {
            state.LoadRoom(true);
            Assert(state.TryActivate(true, existing), $"setup {reason}");
            Assert(state.EndRun() && !state.Active, $"{reason} restores normal decisions");
            Assert(
                !state.TryActivate(true, existing),
                $"{reason} voids a receipt even while the old living encounter still exists"
            );
        }

        state.LoadRoom(true);
        try
        {
            state.TryActivate(true, FailingSnapshot());
            throw new Exception("Expected projectile snapshot failure");
        }
        catch (InvalidOperationException)
        {
            Assert(
                !state.Active && !state.SuppressProjectileBlock(oldProjectile, 10f),
                "failed snapshot does not partially activate the effect"
            );
        }
        Assert(state.TryActivate(true, existing), "failed snapshot can be retried manually");
        Assert(
            state.TryActivate(true, FailingSnapshot()) && state.RemainingSeconds == 60,
            "duplicate receipt never enumerates or snapshots projectiles again"
        );
        state.LoadRoom(false);
        Assert(
            !state.Active && !state.TryActivate(true, existing),
            "Runeforge load ends the effect and voids receipts instead of queuing"
        );

        state.LoadRoom(true);
        Assert(state.TryActivate(true, existing), "setup active room-clear pause");
        state.Advance(20, false);
        state.ClearRoom();
        Assert(state.RoomCleared && state.Active, "last enemy pauses but does not end the effect");
        state.Advance(120, false);
        Assert(
            state.RemainingSeconds == 40,
            "waiting for reward, selecting it and waiting at the exit consume no time"
        );
        state.ClearRoom();
        Assert(
            state.TryActivate(true, existing) && state.RoomCleared && state.RemainingSeconds == 60,
            "repeated clear and another receipt keep the pause and reset to sixty"
        );
        state.Advance(120, false);
        Assert(state.RemainingSeconds == 60, "reset does not resume a cleared room");
        state.LoadRoom(true);
        Assert(!state.RoomCleared && state.Active, "next combat room clears only the between-room pause");
        state.Advance(120, true);
        Assert(state.RemainingSeconds == 60, "next room still waits for loading and player readiness");
        state.Advance(10, false);
        Assert(state.RemainingSeconds == 50, "next ready room resumes the preserved timer");
        state.EndRun();
        state.LoadRoom(true);
        state.ClearRoom();
        Assert(
            !state.Active && state.TryActivate(true, existing),
            "clear is remembered even before a trap is received"
        );
        state.Advance(120, false);
        Assert(state.RemainingSeconds == 60, "first receipt after clear waits for the next room");
        state.LoadRoom(false);
        Assert(
            !state.Active && !state.RoomCleared && !state.TryActivate(true, existing),
            "hub load clears room pause and voids the trap"
        );
        Console.WriteLine("PASS Dodge Only sixty-second timer, pause, reset, projectile boundary and run lifecycle");
    }

    private static IEnumerable<(IntPtr, float)> FailingSnapshot()
    {
        yield return (new IntPtr(123), 10f);
        throw new InvalidOperationException("Simulated interrupted projectile enumeration");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
