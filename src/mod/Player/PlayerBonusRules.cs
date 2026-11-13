namespace UntilYouFallArchipelago;

internal static class PlayerBonusRules
{
    public static readonly long[] ItemIds = ApItems.PlayerBonusCopies.Keys.ToArray();

    public static readonly long[] ReceiptItemIds = ItemIds.Concat(WeaponDamageBonusRules.ItemIds).ToArray();

    public static int MaxCount(long id)
    {
        if (ApItems.PlayerBonusCopies.TryGetValue(id, out var count))
            return count;
        return WeaponDamageBonusRules.IsItem(id) ? ApItems.DamageItemCopies : 0;
    }

    public static int Clamp(long id, int count) => Math.Clamp(count, 0, MaxCount(id));

    public static int Amount(long id, int count) =>
        Clamp(id, count)
        * (
            id == ApItems.DashImpactBonusId ? 5
            : id is ApItems.CriticalWindowBonusId or ApItems.DashRechargeBonusId ? 10
            : 1
        );

    public static float CriticalWindowMultiplier(int count) => 1f + Amount(ApItems.CriticalWindowBonusId, count) / 100f;

    public static float DashRechargeTimeMultiplier(int count) => 1f - Amount(ApItems.DashRechargeBonusId, count) / 100f;

    public static float DashRechargeDelta(float baseTime, int wanted, int applied) =>
        baseTime * (DashRechargeTimeMultiplier(wanted) - DashRechargeTimeMultiplier(applied));

    public static string? Label(long id, int count)
    {
        var amount = Amount(id, count);
        if (amount == 0)
            return null;
        return id switch
        {
            ApItems.MaximumHealthBonusId => $"Maximum Health +{amount}",
            ApItems.DashChargesBonusId => $"Dash Charges +{amount}",
            ApItems.DashImpactBonusId => $"Dash Impact +{amount}",
            ApItems.ComboLimitBonusId => $"Combo Limit +{amount}",
            ApItems.EnhanceSuperBonusId => "Super Enhanced",
            ApItems.CriticalWindowBonusId => $"Critical Strike Window +{amount}%",
            ApItems.DashRechargeBonusId => $"Dash Recharge Time -{amount}%",
            _ => null,
        };
    }

    public static IEnumerable<T> PresentHands<T>(T? right, T? left)
        where T : class
    {
        if (right != null)
            yield return right;
        if (left != null)
            yield return left;
    }

    public static bool IsNativeBonusRow(string name) => name.StartsWith("StatBuffTemplate", StringComparison.Ordinal);

    public static float PlayerHeaderPosition(float listRootPosition, float lastRowPosition, float rowHeight) =>
        listRootPosition + lastRowPosition - rowHeight;

    public static bool CanApplyBonuses(bool playerReady) => playerReady;

    public static int RemainingApplications(int wanted, int applied) => Math.Max(0, wanted - applied);

    public const float FallbackMaxHealthCap = 10f;

    public static int AllowedMaxHealthApplications(
        float cap,
        float currentMaxHealth,
        float amountPerCopy,
        int remaining
    ) =>
        amountPerCopy <= 0f
            ? remaining
            : Math.Clamp((int)MathF.Floor((cap - currentMaxHealth) / amountPerCopy), 0, remaining);

    public static bool CanEnhanceSuper(int abilityLevel, int maxAbilityLevel) =>
        maxAbilityLevel > 0 && abilityLevel < maxAbilityLevel - 1;

    public static bool IsSuperMaxed(int abilityLevel, int maxAbilityLevel) =>
        maxAbilityLevel > 0 && abilityLevel >= maxAbilityLevel;
}
