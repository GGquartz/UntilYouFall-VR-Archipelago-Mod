using UntilYouFallArchipelago;

internal static class SingleWeaponRewardTests
{
    public static void Run()
    {
        Assert(
            !SingleWeaponRewardRules.AllowWeapon(true, false),
            "empty AP hand must reject weapon traits before reward filtering dereferences its armament"
        );
        Assert(SingleWeaponRewardRules.AllowWeapon(true, true), "equipped hand retains native eligibility");
        Assert(SingleWeaponRewardRules.AllowWeapon(false, false), "non-AP eligibility remains native");
        Assert(
            SingleWeaponRewardRules.CompleteSafely(true, true, false, true),
            "applying a trait to the only weapon must bypass native dereference of the other armament"
        );
        Assert(!SingleWeaponRewardRules.CompleteSafely(true, true, true, true), "two weapons keep native completion");
        Assert(
            !SingleWeaponRewardRules.CompleteSafely(true, true, false, false),
            "health and currency keep native completion"
        );
        Assert(
            !SingleWeaponRewardRules.CompleteSafely(true, false, true, true),
            "empty hand cannot apply a weapon trait"
        );
        Assert(!SingleWeaponRewardRules.CompleteSafely(false, true, false, true), "non-AP completion remains native");
        Console.WriteLine("PASS: single-weapon reward eligibility, safe completion and ordinary rewards");
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok)
            throw new Exception("Single weapon rewards: " + message);
    }
}
