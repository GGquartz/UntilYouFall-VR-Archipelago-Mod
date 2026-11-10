namespace UntilYouFallArchipelago;

internal static class WeaponAnalyticsRules
{
    public static bool AllowPair(bool leftHasWeapon, bool rightHasWeapon, bool sameWeapon = false) =>
        leftHasWeapon && rightHasWeapon && !sameWeapon;

    public static bool AllowHand(bool handHasWeapon) => handHasWeapon;
}
