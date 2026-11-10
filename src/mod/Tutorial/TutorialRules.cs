namespace UntilYouFallArchipelago;

internal static class TutorialRules
{
    public const string ReactiveSupersRoomGuid = "86b0fd086b7e68f4899980230cafa87a";
    public const string ReactiveDodgingRoomGuid = "5e66ee94b96fc634d9ade1049cbdae2c";

    public static bool IsTutorialPopup(string? name) =>
        name != null
        && (
            name.StartsWith("PopupUIAsset_Tutorial_", StringComparison.Ordinal)
            || name.StartsWith("PopupUIAsset_ReactiveTutorial_", StringComparison.Ordinal)
        );

    public static bool IsBattleTip(string? name) =>
        name != null
        && (
            name.StartsWith("PopupUI_BattleTip_", StringComparison.Ordinal)
            || name.StartsWith("PopupUIAsset_BattleTip_", StringComparison.Ordinal)
        );

    public static bool ShouldReturnToRuneforge(
        bool connected,
        bool loading,
        string? campaign,
        string? tutorial,
        string? roomGuid = null
    ) =>
        connected
        && !loading
        && (
            (!string.IsNullOrEmpty(tutorial) && string.Equals(campaign, tutorial, StringComparison.OrdinalIgnoreCase))
            || (
                string.Equals(campaign, StageChecks.CampaignGuid, StringComparison.OrdinalIgnoreCase)
                && IsReactiveRoom(roomGuid)
            )
        );

    public static bool IsReactiveRoom(string? guid) =>
        string.Equals(guid, ReactiveSupersRoomGuid, StringComparison.OrdinalIgnoreCase)
        || string.Equals(guid, ReactiveDodgingRoomGuid, StringComparison.OrdinalIgnoreCase);
}
