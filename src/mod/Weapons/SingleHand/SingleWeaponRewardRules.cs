namespace UntilYouFallArchipelago;

internal static class SingleWeaponRewardRules
{
    public static bool AllowWeapon(bool apLoadout, bool hasWeapon) => !apLoadout || hasWeapon;

    public static bool CompleteSafely(bool apLoadout, bool hasWeapon, bool hasOtherWeapon, bool hasTrait) =>
        apLoadout && hasWeapon && !hasOtherWeapon && hasTrait;
}
