using UntilYouFallArchipelago;

internal static class WeaponAnalyticsTests
{
    public static void Run()
    {
        Assert(
            !WeaponAnalyticsRules.AllowPair(false, true),
            "a right-hand starter with empty left hand must not enter telemetry that reads both definitions"
        );
        Assert(
            !WeaponAnalyticsRules.AllowPair(true, false),
            "a left-hand starter with empty right hand must not enter telemetry that reads both definitions"
        );
        Assert(!WeaponAnalyticsRules.AllowPair(false, false), "loading with empty hands skips pair telemetry");
        Assert(WeaponAnalyticsRules.AllowPair(true, true), "two equipped weapons retain normal telemetry");
        Assert(
            !WeaponAnalyticsRules.AllowPair(true, true, true),
            "the same weapon in both hands must skip run telemetry (duplicate grip_time key)"
        );
        Assert(!WeaponAnalyticsRules.AllowHand(false), "death telemetry must not dereference an empty selected hand");
        Assert(WeaponAnalyticsRules.AllowHand(true), "death telemetry retains an equipped selected hand");
        Console.WriteLine(
            "PASS: AP telemetry guards for either empty hand, loading, two weapons and selected-hand death"
        );
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok)
            throw new Exception("Weapon analytics: " + message);
    }
}
