namespace UntilYouFallArchipelago;

internal static class SuperChargeRules
{
    public static float? Amount(float maximum, float multiplier)
    {
        if (!float.IsFinite(maximum) || maximum <= 0 || !float.IsFinite(multiplier) || multiplier <= 0)
            return null;

        var amount = maximum / Math.Min(1f, multiplier);
        return float.IsFinite(amount) ? amount : null;
    }
}
