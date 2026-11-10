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

    public static ApLogoColor DisplayColor(ApLogoColor color)
    {
        var maximum = Math.Max(color.R, Math.Max(color.G, color.B));

        return maximum > 0f ? new ApLogoColor(color.R / maximum, color.G / maximum, color.B / maximum, color.A) : color;
    }

    public static ApLogoColor Emission(ApLogoColor color) => new(color.R * 10f, color.G * 10f, color.B * 10f, color.A);

    public static bool KeepOriginalRenderer(int rendererIndex, int sourceRendererIndex) =>
        rendererIndex != sourceRendererIndex;
}
