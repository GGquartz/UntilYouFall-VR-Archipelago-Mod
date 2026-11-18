namespace UntilYouFallArchipelago;

internal static class ApLogoVisualRules
{
    public static readonly string[] ColorProperties = ["_Color", "_BaseColor", "_TintColor"];
    public static readonly string[] EmissionProperties = ["_EmissionColor", "_EMISSIONCOLOR"];
    public const string AlbedoTextureProperty = "_AlbedoThickness";
    public const string AlbedoContributionProperty = "_Vector1_AlbedoContribution";
    public const float AlbedoContribution = 1f;
    public const string PulseFloorProperty = "_Vector1_PulseIntensityFloor";
    public const float PulseFloor = 1f;
    public const float LocalScaleX = -1f;
    public const float EmissionIntensity = 10f;
    public const float LumaRed = 0.299f;
    public const float LumaGreen = 0.587f;
    public const float LumaBlue = 0.114f;

    public static ApLogoColor DisplayColor(ApLogoColor color)
    {
        var maximum = Math.Max(color.R, Math.Max(color.G, color.B));

        return maximum > 0f ? new ApLogoColor(color.R / maximum, color.G / maximum, color.B / maximum, color.A) : color;
    }

    public static ApLogoColor Grayscale(ApLogoColor color)
    {
        var luma = color.R * LumaRed + color.G * LumaGreen + color.B * LumaBlue;

        return new ApLogoColor(luma, luma, luma, color.A);
    }

    public static ApLogoColor Emission(ApLogoColor color) =>
        new(color.R * EmissionIntensity, color.G * EmissionIntensity, color.B * EmissionIntensity, color.A);

    public static bool KeepOriginalRenderer(int rendererIndex, int sourceRendererIndex) =>
        rendererIndex != sourceRendererIndex;
}
