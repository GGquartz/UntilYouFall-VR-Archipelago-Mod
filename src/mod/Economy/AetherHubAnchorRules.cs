namespace UntilYouFallArchipelago;

internal static class AetherHubAnchorRules
{
    public static bool ContainsDisplayedValue(string value, int currency)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length > 0 && int.TryParse(digits, out var displayed) && displayed == currency;
    }

    public static int Score(bool variableMatch, bool active, bool worldRenderable, bool numericMatch, bool nameHint)
    {
        if (!active || !worldRenderable)
            return int.MinValue;

        return (variableMatch ? 1000 : 0) + (numericMatch ? 100 : 0) + (nameHint ? 10 : 0);
    }

    public static float OffsetBelow(float sourceHeight) => Math.Clamp(sourceHeight * 0.45f, 0.06f, 0.2f);
}
