namespace UntilYouFallArchipelago;

internal static class WeaponUpgradeCapTests
{
    public static void Run()
    {
        Assert(
            WeaponUpgradeCapRules.MaximumDisplayedLevel(0) == 1,
            "no progressive items cap every weapon at displayed level 1"
        );
        Assert(
            WeaponUpgradeCapRules.MaximumDisplayedLevel(1) == 2 && WeaponUpgradeCapRules.MaximumDisplayedLevel(9) == 10,
            "each of nine progressive items raises the displayed cap once"
        );
        Assert(
            WeaponUpgradeCapRules.MaximumDisplayedLevel(20) == 10,
            "extra copies cannot raise the cap above level 10"
        );
        Assert(
            WeaponUpgradeCapRules.AllowsInternalLevel(0, 0) && !WeaponUpgradeCapRules.AllowsInternalLevel(1, 0),
            "displayed level 1 is native level 0 and the first upgrade starts locked"
        );
        Assert(
            WeaponUpgradeCapRules.AllowsCrushStart(1, null),
            "purchase crush remains native before AP slot state is known"
        );
        Assert(
            !WeaponUpgradeCapRules.AllowsCrushStart(1, 0),
            "purchase crush cannot start when its level exceeds the cap"
        );
        Assert(
            WeaponUpgradeCapRules.AllowsCrushStart(1, 1),
            "purchase crush can start after the cap reaches its target level"
        );
        Assert(
            WeaponUpgradeCapRules.AllowsInternalLevel(9, 9) && !WeaponUpgradeCapRules.AllowsInternalLevel(10, 9),
            "nine items permit native level 9 but never level 10"
        );
        Assert(
            WeaponUpgradeCapRules.Message(4) == "Maximum upgrade level is 5",
            "blocked upgrades report the displayed cap"
        );
        Assert(
            WeaponUpgradeCapRules.AltarLabel(0) == "Max Level: I"
                && WeaponUpgradeCapRules.AltarLabel(4) == "Max Level: V"
                && WeaponUpgradeCapRules.AltarLabel(9) == "Max Level: X",
            "upgrade altar displays the cap with the game's Roman level numbering"
        );
        Assert(
            WeaponUpgradeCapRules.IsUpgradeAltarHierarchy("Label", "Dynamic Elements", "UpgradeStation", "Interactions")
                && !WeaponUpgradeCapRules.IsUpgradeAltarHierarchy(
                    "UpgradeInstructions",
                    "PurchasePanel",
                    "TitlePanel",
                    "TitlePanelParent"
                ),
            "only the native Runeforge world label is targeted"
        );
        Assert(
            WeaponUpgradeCapRules.IsBlockedUpgrade(1, 0)
                && !WeaponUpgradeCapRules.IsBlockedUpgrade(1, 1)
                && !WeaponUpgradeCapRules.IsBlockedUpgrade(1, null),
            "only an upgrade above the known AP cap is blocked"
        );
        Assert(
            WeaponUpgradeCapRules.MaximumInternalLevel(0) == 0
                && WeaponUpgradeCapRules.MaximumInternalLevel(4) == 4
                && WeaponUpgradeCapRules.MaximumInternalLevel(9) == 9
                && WeaponUpgradeCapRules.MaximumInternalLevel(20) == 9,
            "internal cap mirrors displayed cap minus one and never exceeds nine"
        );
        Assert(
            WeaponUpgradeCapRules.CapNativeMaxUpgradeLevel(9, null) == 9,
            "native MaxUpgradeLevel is preserved before AP slot state is known"
        );
        Assert(
            WeaponUpgradeCapRules.CapNativeMaxUpgradeLevel(9, 0) == 0
                && WeaponUpgradeCapRules.CapNativeMaxUpgradeLevel(9, 4) == 4
                && WeaponUpgradeCapRules.CapNativeMaxUpgradeLevel(9, 9) == 9,
            "AP cap lowers the native MaxUpgradeLevel so the game shows Maximum Level Reached at the AP cap"
        );
        Assert(
            WeaponUpgradeCapRules.CapNativeMaxUpgradeLevel(3, 9) == 3,
            "AP cap never raises the weapon's natural MaxUpgradeLevel"
        );
        Console.WriteLine("PASS: progressive permanent weapon upgrade cap");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
