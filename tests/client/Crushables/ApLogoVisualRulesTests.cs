using UntilYouFallArchipelago;

internal static class ApLogoVisualRulesTests
{
    public static void Run()
    {
        Assert(
            ApLogoVisualRules.ColorProperties.SequenceEqual(["_Color", "_BaseColor", "_TintColor"]),
            "uses supported color property order"
        );
        Assert(
            ApLogoVisualRules.EmissionProperties.SequenceEqual(["_EmissionColor", "_EMISSIONCOLOR"]),
            "supports standard and verified crystal emission properties"
        );

        var display = ApLogoVisualRules.DisplayColor(new ApLogoColor(0.25f, 0.5f, 0.75f, 0.4f));
        Assert(
            Math.Abs(display.R - 1f / 3f) < 0.0001f
                && Math.Abs(display.G - 2f / 3f) < 0.0001f
                && Math.Abs(display.B - 1f) < 0.0001f
                && Math.Abs(display.A - 0.4f) < 0.0001f,
            "display colors preserve hue while lifting the strongest channel to full intensity"
        );
        var emission = ApLogoVisualRules.Emission(display);
        Assert(
            Math.Abs(emission.R - 10f / 3f) < 0.0001f
                && Math.Abs(emission.G - 20f / 3f) < 0.0001f
                && Math.Abs(emission.B - 10f) < 0.0001f
                && Math.Abs(emission.A - 0.4f) < 0.0001f,
            "emission strengthens RGB while preserving alpha"
        );
        Assert(
            !ApLogoVisualRules.KeepOriginalRenderer(0, 0) && ApLogoVisualRules.KeepOriginalRenderer(1, 0),
            "only the original crystal mesh is replaced; effect renderers remain"
        );
        Assert(
            ApLogoVisualRules.AlbedoContributionProperty == "_Vector1_AlbedoContribution"
                && Math.Abs(ApLogoVisualRules.AlbedoContribution - 1f) < 0.0001f,
            "the custom AP albedo contributes fully to the crystal shader"
        );
        Assert(
            ApLogoVisualRules.PulseFloorProperty == "_Vector1_PulseIntensityFloor"
                && Math.Abs(ApLogoVisualRules.PulseFloor - 1f) < 0.0001f,
            "the AP emission colors remain visible throughout the pulse"
        );
        Assert(
            Math.Abs(ApLogoVisualRules.LocalScaleX + 1f) < 0.0001f,
            "the imported logo is mirrored back to its intended left-right orientation"
        );
        Assert(
            ApLogoVisualRules.AlbedoTextureProperty == "_AlbedoThickness",
            "the strongly cyan source albedo can be replaced independently of crystal detail maps"
        );
        Console.WriteLine("PASS: AP logo material color and emission rules");
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok)
            throw new Exception("AP logo visual rules: " + message);
    }
}
