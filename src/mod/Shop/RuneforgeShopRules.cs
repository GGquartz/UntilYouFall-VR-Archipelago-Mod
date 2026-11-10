namespace UntilYouFallArchipelago;

internal static class RuneforgeShopRules
{
    public const string RuneforgeRoomGuid = "941bc191029c23942a30e8f1668b8614";
    public const string PrefabResource = "Crushables/Rewards/Reward_Aether_Lvl01_HQ";
    public const int VisualRetryDelayMs = 16;
    public const float InspectionPanelWidth = 650f;
    public const float InspectionPanelHeight = 150f;
    public const float InspectionPanelOffsetY = 0f;
    public const float CrushSeconds = 2.5f;

    public static bool InRuneforge(string? roomGuid, bool loading, bool coreLoading) =>
        roomGuid == RuneforgeRoomGuid && !loading && !coreLoading;

    public static bool ShouldAttachVisual(bool hasReward, bool hasVisual, bool retryDue) =>
        hasReward && !hasVisual && retryDue;

    public const float ShopItemHover = 0.15f;
    public const string DefaultPricePrefix = "<sprite=\"StatIcons_SpriteAtlas\" index=6 tint=1>";

    public static bool IsPreviewLocked(int section, int unlockCount) => !ShopChecks.IsUnlocked(section, unlockCount);

    public static string PriceText(string? nativeText, int price)
    {
        var end = nativeText?.IndexOf('>') ?? -1;
        var prefix =
            end > 0 && nativeText!.StartsWith("<sprite", StringComparison.Ordinal)
                ? nativeText[..(end + 1)]
                : DefaultPricePrefix;

        return prefix + price;
    }
}
