namespace UntilYouFallArchipelago;

internal static partial class ApItems
{
    public const int VitalityHealAmount = ShardOfVitalityAmount;

    public static bool IsHand(long id) => id == LeftHandId || id == RightHandId;

    public static bool IsRunOnlyItem(long id) => id == SuperChargeId || id == EnemyStasisId;

    public static bool IsTraitSlot(long id) => WeaponCatalog.ByTraitSlotId(id) != null;

    public static bool IsWeaponAbility(long id) => WeaponAbilityCatalog.ItemIds.Values.Contains(id);

    public static bool IsAetherFiller(long id) => id == AetherFillerId;

    public static bool IsAetherMultiplier(long id) => id == ProgressiveAetherMultiplierId;

    public static bool IsPlayerBonus(long id) => PlayerBonusRules.MaxCount(id) > 0;

    public static int? AetherBundleAmount(long id) => AetherBundles.TryGetValue(id, out var amount) ? amount : null;
}
