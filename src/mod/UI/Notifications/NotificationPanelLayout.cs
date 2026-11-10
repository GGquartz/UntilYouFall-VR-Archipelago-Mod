namespace UntilYouFallArchipelago;

internal static class NotificationPanelLayout
{
    public const string NotificationResource = "UI/CashOut_View_HQ";
    public const string NotificationPanelName = "ForgeNotificationPanel";
    public const float ApVerticalPosition = -0.25f;
    public const float TechnicalVerticalPosition = -0.37f;
    public const float PanelWidth = 1000f;
    public const float HorizontalPadding = 60f;
    public const float VerticalPadding = 10f;

    public static float HeightFor(float preferredTextHeight, bool hasLocation) =>
        Math.Max(hasLocation ? 120f : 70f, preferredTextHeight + VerticalPadding * 2f);

    public static float VerticalOffsetFor(float textBoundsCenterY) => -textBoundsCenterY;
}
