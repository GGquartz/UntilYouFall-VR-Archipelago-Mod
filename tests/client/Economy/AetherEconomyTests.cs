using UntilYouFallArchipelago;

internal static class AetherEconomyTests
{
    public static void Run()
    {
        AssertFillerAmount();
        AssertFillerGate();
        AssertMultiplierBonus();
        AssertMultiplierScale();
        AssertMultiplierLabel();
        AssertHubAnchorSelection();
        AssertRewardScale();
        AssertStatePersistence();
        Console.WriteLine(
            "PASS: Aether filler amount/gate, multiplier bonus/scale, hub label, reward scale table and multiplier persistence"
        );
    }

    private static void AssertFillerAmount()
    {
        Assert(AetherFillerRules.Amount == 100, "Aether filler grants 100");
        Assert(
            AetherFillerRules.Apply(0) == 100 && AetherFillerRules.Apply(5000) == 5100,
            "Aether filler adds its amount to the current currency"
        );
    }

    private static void AssertFillerGate()
    {
        Assert(
            !AetherFillerRules.CanApply(connected: false, hasCurrency: true),
            "disconnected session cannot apply filler"
        );
        Assert(
            !AetherFillerRules.CanApply(connected: true, hasCurrency: false),
            "missing currency variable defers filler"
        );
        Assert(
            AetherFillerRules.CanApply(connected: true, hasCurrency: true),
            "connected session with live currency applies filler"
        );
    }

    private static void AssertMultiplierBonus()
    {
        Assert(AetherMultiplierRules.MaxItemCount == 5, "five progressive aether multiplier copies");
        Assert(Math.Abs(AetherMultiplierRules.Bonus(0) - 0f) < 1e-6f, "zero copies add no bonus");
        Assert(Math.Abs(AetherMultiplierRules.Bonus(3) - 0.6f) < 1e-6f, "three copies add +0.6");
        Assert(Math.Abs(AetherMultiplierRules.Bonus(10) - 1.0f) < 1e-6f, "bonus is capped at five copies");
        Assert(
            Math.Abs(AetherMultiplierRules.EffectiveMultiplier(1.0f, 5) - 2.0f) < 1e-6f,
            "Easy difficulty with five copies doubles the payout multiplier"
        );
        Assert(
            Math.Abs(AetherMultiplierRules.EffectiveMultiplier(1.2f, 5) - 2.2f) < 1e-6f,
            "Nightmare with five copies yields 2.2x payout multiplier"
        );
    }

    private static void AssertMultiplierScale()
    {
        // Easy difficulty: vanilla amount already includes payout 1.0, so base equals amount.
        Assert(AetherMultiplierRules.Scale(125, 1.0f, 0) == 125, "no copies leave the vanilla amount unchanged");
        Assert(AetherMultiplierRules.Scale(125, 1.0f, 5) == 250, "five copies on Easy double a 125 reward");
        // Nightmare: vanilla amount already multiplied by 1.2.
        Assert(AetherMultiplierRules.Scale(300, 1.2f, 4, 3) == 825, "Nightmare 2.5x + 4 copies (0.8) = 3.3x");
        Assert(AetherMultiplierRules.Scale(138, 1.1f, 0, 1) == 176, "Challenging overrides native 1.1x with 1.4x");
        Assert(AetherMultiplierRules.Scale(144, 1.15f, 0, 2) == 225, "Relentless overrides native 1.15x with 1.8x");
        Assert(AetherMultiplierRules.Scale(300, 1.2f, 0, 3) == 625, "Nightmare overrides native 1.2x with 2.5x");
        Assert(AetherMultiplierRules.Scale(350, 1.4f, 0, 3) == 675, "Avarice stays additive: 250 * (1.4 - 1.2 + 2.5)");
        Assert(
            AetherMultiplierRules.RewriteGain("x Aether Gain increased by 10% y", 1)
                == "x Aether Gain increased by 1.4x y",
            "native gain text shows the overriding total"
        );
        Assert(AetherMultiplierRules.RewriteGain("no gain line", 3) == "no gain line", "other text is untouched");
    }

    private static void AssertRewardScale()
    {
        Assert(AetherRewardScaleRules.ScaleFor(15) == 100, "shard scales to 125");
        Assert(AetherRewardScaleRules.ScaleFor(25) == 150, "fragment scales to 200");
        Assert(AetherRewardScaleRules.ScaleFor(50) == 200, "crystal scales to 400");
        Assert(AetherRewardScaleRules.ScaleFor(100) == 500, "cluster scales to 800");
        Assert(AetherRewardScaleRules.ScaleFor(250) == 1000, "blossom scales to 1500");
        Assert(AetherRewardScaleRules.ScaleFor(1000) == null, "roomMotes and bosses stay vanilla");
        Assert(AetherRewardScaleRules.ScaleFor(42) == null, "unknown amounts stay vanilla");
    }

    private static void AssertStatePersistence()
    {
        PersistImpl();
    }

    private static void AssertMultiplierLabel()
    {
        Assert(
            Math.Abs(AetherMultiplierRules.DisplayMultiplier(0) - 1.0f) < 1e-6f,
            "zero AP copies display 1.0x independently of difficulty"
        );
        Assert(Math.Abs(AetherMultiplierRules.DisplayMultiplier(3) - 1.6f) < 1e-6f, "three AP copies display 1.6x");
        Assert(
            AetherMultiplierRules.FormatDisplay(0) == "Aether Multiplier: 1.0x",
            "hub label formats the base AP multiplier"
        );
        Assert(
            AetherMultiplierRules.FormatDisplay(5) == "Aether Multiplier: 2.0x",
            "hub label formats the capped AP multiplier"
        );
    }

    private static void AssertHubAnchorSelection()
    {
        Assert(
            AetherHubAnchorRules.ContainsDisplayedValue("50,000", 50000),
            "formatted currency text matches its value"
        );
        Assert(!AetherHubAnchorRules.ContainsDisplayedValue("Upgrade 10", 50000), "unrelated numeric text is rejected");
        Assert(
            AetherHubAnchorRules.Score(
                variableMatch: true,
                active: true,
                worldRenderable: true,
                numericMatch: true,
                nameHint: false
            )
                > AetherHubAnchorRules.Score(
                    variableMatch: false,
                    active: true,
                    worldRenderable: true,
                    numericMatch: true,
                    nameHint: true
                ),
            "the currency-bound component wins over name-based fallback candidates"
        );
        Assert(
            AetherHubAnchorRules.Score(
                variableMatch: true,
                active: false,
                worldRenderable: true,
                numericMatch: true,
                nameHint: true
            ) == int.MinValue,
            "inactive candidates cannot be selected"
        );
        Assert(
            Math.Abs(AetherHubAnchorRules.OffsetBelow(0.2f) - 0.09f) < 1e-6f,
            "label offset scales with the source height"
        );
        Assert(Math.Abs(AetherHubAnchorRules.OffsetBelow(0f) - 0.06f) < 1e-6f, "label offset has a readable minimum");
    }

    private static void PersistImpl()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-ap-aether-" + Guid.NewGuid());
        try
        {
            var state = ApState.Open(directory, "aether/seed/team0/slot1");
            state.SetAetherMultiplierItemCount(2);
            state.SetAetherMultiplierItemCount(2);
            var reloaded = ApState.Open(directory, "aether/seed/team0/slot1");
            Assert(reloaded.AetherMultiplierItemCount == 2, "aether multiplier count persists idempotently");
            reloaded.SetAetherMultiplierItemCount(1);
            Assert(reloaded.AetherMultiplierItemCount == 2, "aether multiplier count is monotonic");
            reloaded.SetAetherMultiplierItemCount(99);
            Assert(
                ApState.Open(directory, "aether/seed/team0/slot1").AetherMultiplierItemCount == 5,
                "aether multiplier count is capped at the maximum"
            );
            var other = ApState.Open(directory, "aether/other-seed/team0/slot1");
            Assert(other.AetherMultiplierItemCount == 0, "multiplier count is isolated by seed");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok)
            throw new Exception("Aether economy: " + message);
    }
}
