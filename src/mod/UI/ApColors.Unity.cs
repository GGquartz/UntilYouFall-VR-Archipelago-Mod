using UnityEngine;

namespace UntilYouFallArchipelago;

internal static partial class ApColors
{
    public static readonly Color SalmonColor = new(0.98f, 0.502f, 0.447f, 1f);
    public static readonly Color GoldColor = new(0.95f, 0.72f, 0.18f, 1f);
    public static readonly Color ModAccentColor = new(0.65f, 0.9f, 1f, 1f);
    public static readonly Color AnchoredLabelGoldColor = new(1f, 0.72f, 0.18f, 1f);

    public static readonly Color QuestBoardAccentColor = new(0.45f, 0.8f, 1f, 1f);
    public static readonly Color QuestBoardMutedTextColor = new(0.62f, 0.62f, 0.62f, 1f);
    public static readonly Color QuestBoardDescriptionColor = new(0.85f, 0.85f, 0.85f, 1f);
    public static readonly Color QuestBoardCardBackColor = new(0.04f, 0.04f, 0.05f, 1f);
    public static readonly Color QuestBoardIconBackColor = new(0.15f, 0.2f, 0.3f, 1f);
    public static readonly Color QuestBoardBarBackColor = new(0.3f, 0.3f, 0.34f, 1f);
    public static readonly Color QuestBoardButtonColor = new(0.12f, 0.12f, 0.14f, 1f);
    public static readonly Color QuestBoardButtonHoverColor = new(0.18f, 0.3f, 0.42f, 1f);
    public static readonly Color QuestBoardFallbackBackColor = new(0f, 0f, 0f, 0.9f);

    public static readonly Color TimedTrapDefaultColor = new(0.75f, 0.2f, 0.16f, 1f);
    public static readonly Color TimedTrapBackgroundColor = new(0.3f, 0.35f, 0.4f, 0.6f);
    public static readonly Color NotificationBackgroundColor = new(0f, 0f, 0f, 0.75f);
    public static readonly Color WristConsoleBackgroundColor = new(0f, 0f, 0f, 0.45f);

    public static readonly Color ShopStandGlowColor = new(4.5f, 0.6f, 9f, 0f);
    public static readonly Color ShopStandTintColor = new(0.6f, 0.25f, 1f, 1f);

    public static Color WithAlpha(this Color color, float alpha) => new(color.r, color.g, color.b, alpha);
}
