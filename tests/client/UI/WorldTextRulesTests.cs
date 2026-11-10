using UntilYouFallArchipelago;

internal static class WorldTextRulesTests
{
    public static void Run()
    {
        Assert(WorldTextRules.FirstVisibleLayer(1 << 0) == 0, "default layer is selected");
        Assert(WorldTextRules.FirstVisibleLayer(1 << 5) == 5, "UI layer is selected");
        Assert(
            WorldTextRules.FirstVisibleLayer((1 << 3) | (1 << 7)) == 3,
            "lowest visible layer is selected deterministically"
        );
        Assert(WorldTextRules.FirstVisibleLayer(0) == -1, "empty culling mask is rejected");
        Console.WriteLine("PASS: world text layer and occlusion rules");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception("World text test: " + message);
    }
}
