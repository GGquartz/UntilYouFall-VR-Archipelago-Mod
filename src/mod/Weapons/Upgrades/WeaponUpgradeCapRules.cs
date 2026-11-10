namespace UntilYouFallArchipelago;

internal static class WeaponUpgradeCapRules
{
    public const int MaximumItemCount = ApItems.ProgressiveWeaponUpgradeLevelMaxCount;
    private const string AltarPrefix = "Max Level: ";
    private static readonly string[] RomanLevels = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X"];

    public static int MaximumDisplayedLevel(int itemCount) => Math.Clamp(itemCount, 0, MaximumItemCount) + 1;

    public static int MaximumInternalLevel(int itemCount) => Math.Clamp(itemCount, 0, MaximumItemCount);

    public static int CapNativeMaxUpgradeLevel(int nativeMax, int? itemCount) =>
        itemCount.HasValue ? Math.Min(nativeMax, MaximumInternalLevel(itemCount.Value)) : nativeMax;

    public static bool AllowsInternalLevel(int targetLevel, int itemCount) =>
        targetLevel <= Math.Clamp(itemCount, 0, MaximumItemCount);

    public static bool AllowsCrushStart(int targetLevel, int? itemCount) =>
        !itemCount.HasValue || AllowsInternalLevel(targetLevel, itemCount.Value);

    public static string Message(int itemCount) => $"Maximum upgrade level is {MaximumDisplayedLevel(itemCount)}";

    public static string AltarLabel(int itemCount) => AltarPrefix + RomanLevels[MaximumDisplayedLevel(itemCount) - 1];

    public static bool IsUpgradeAltarHierarchy(
        string name,
        string? parentName,
        string? grandparentName,
        string? greatGrandparentName
    ) =>
        name == "Label"
        && parentName == "Dynamic Elements"
        && grandparentName == "UpgradeStation"
        && greatGrandparentName == "Interactions";

    public static bool IsBlockedUpgrade(int targetLevel, int? itemCount) => !AllowsCrushStart(targetLevel, itemCount);
}
