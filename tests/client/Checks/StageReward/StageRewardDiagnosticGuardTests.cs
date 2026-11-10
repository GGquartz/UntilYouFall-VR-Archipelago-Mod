using UntilYouFallArchipelago;

internal static class StageRewardDiagnosticGuardTests
{
    public static void Run()
    {
        var completed = false;
        Exception? captured = null;
        StageRewardDiagnosticGuard.Run(() => completed = true, exception => captured = exception);
        Assert(completed && captured == null, "successful diagnostic action completes without an error callback");

        StageRewardDiagnosticGuard.Run(
            () => throw new NullReferenceException("missing diagnostic metadata"),
            exception => captured = exception
        );
        Assert(
            captured is NullReferenceException,
            "diagnostic metadata failures are reported without escaping into the game lifecycle"
        );

        Console.WriteLine("PASS: Stage Reward diagnostic failures cannot interrupt reward initialization");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception("Stage Reward diagnostic guard: " + message);
    }
}
