using UntilYouFallArchipelago;

internal static class DebugAetherTests
{
    public static void Run()
    {
        Assert(
            DebugAetherRules.GrantAmount == 50_000 && DebugAetherRules.Apply(100) == 50_100,
            "new profiles receive 50,000 additional Aether"
        );
        Assert(
            DebugAetherRules.ShouldGrant(true, true),
            "enabled debug grants a profile captured as new before the save is created"
        );
        Assert(!DebugAetherRules.ShouldGrant(true, false), "existing profiles are unchanged");
        Assert(!DebugAetherRules.ShouldGrant(false, true), "disable marker suppresses the grant");
        Console.WriteLine("PASS: debug Aether amount, new-profile gate and disable override");
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok)
            throw new Exception("Debug Aether: " + message);
    }
}
