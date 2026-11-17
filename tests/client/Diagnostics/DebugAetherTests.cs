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
        var harmony = new HarmonyLib.Harmony();
        DebugAether.Install(harmony, true, _ => { });
        var profile = new Il2CppSG.Claymore.UI.ProfileUI
        {
            _isNewGame = true,
            _currencyVariable = new() { Value = 123 },
            _saveManager = new(),
        };
        var grant = typeof(DebugAether).GetMethod(
            "Grant",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
        )!;
        grant.Invoke(null, [profile, true, false]);
        Assert(
            profile._currencyVariable.Value == 123 && profile._saveManager.EconomySaveCount == 0,
            "blocked profile entry grants no currency and writes no save"
        );
        grant.Invoke(null, [profile, true, true]);
        Assert(
            profile._currencyVariable.Value == 50_123 && profile._saveManager.EconomySaveCount == 1,
            "normal new-profile load preserves the debug grant"
        );
        Console.WriteLine("PASS: debug Aether amount, new-profile gate and disable override");
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok)
            throw new Exception("Debug Aether: " + message);
    }
}
