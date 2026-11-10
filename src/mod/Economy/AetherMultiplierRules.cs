namespace UntilYouFallArchipelago;

internal static class AetherMultiplierRules
{
    public const int MaxItemCount = ApItems.ProgressiveAetherMultiplierMaxCount;
    public const float PerItem = 0.2f;

    public static int Clamp(int count) =>
        count < 0 ? 0
        : count > MaxItemCount ? MaxItemCount
        : count;

    public static float Bonus(int count) => Clamp(count) * PerItem;

    public static float DisplayMultiplier(int count) => 1f + Bonus(count);

    public static string FormatDisplay(int count) =>
        "Aether Multiplier: "
        + DisplayMultiplier(count).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
        + "x";

    public static float EffectiveMultiplier(float vanillaPayout, int apCount) => vanillaPayout + Bonus(apCount);

    private static readonly float[] DifficultyTotals = [1.0f, 1.4f, 1.8f, 2.5f];
    private static readonly float[] NativeDifficultyPayouts = [1.0f, 1.1f, 1.15f, 1.2f];

    private static int Level(int level) =>
        level < 0 ? 0
        : level >= DifficultyTotals.Length ? DifficultyTotals.Length - 1
        : level;

    public static float DifficultyTotal(int level) => DifficultyTotals[Level(level)];

    public static float DifficultyPayout(float vanillaPayout, int level) =>
        vanillaPayout - NativeDifficultyPayouts[Level(level)] + DifficultyTotals[Level(level)];

    public static string RewriteGain(string text, int level) =>
        System.Text.RegularExpressions.Regex.Replace(
            text,
            @"Aether Gain increased by \d+%",
            _ =>
                "Aether Gain increased by "
                + DifficultyTotal(level).ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture)
                + "x"
        );

    public static float Payout(float vanillaPayout, int apCount, int level) =>
        DifficultyPayout(vanillaPayout, level) + Bonus(apCount);

    public static int Scale(int vanillaAmount, float vanillaPayout, int apCount, int level = 0)
    {
        if (vanillaPayout <= 0f)
            return vanillaAmount;
        var baseAmount = vanillaAmount / vanillaPayout;

        return (int)System.Math.Round(baseAmount * Payout(vanillaPayout, apCount, level));
    }
}
