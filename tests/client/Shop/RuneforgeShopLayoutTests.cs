using UntilYouFallArchipelago;

internal static class RuneforgeShopLayoutTests
{
    public static void Run()
    {
        Assert(RuneforgeShopLayout.Sections.Length == 5, "one pose per shop section");
        Assert(RuneforgeShopLayout.FrontColumns.Length == 2, "two added front columns per section");
        Assert(
            RuneforgeShopLayout
                .Sections.Concat(RuneforgeShopLayout.FrontColumns)
                .All(pose =>
                    new[] { pose.Position, pose.Rotation, pose.Scale }.All(IsFinite)
                    && pose.Scale is { X: > 0, Y: > 0, Z: > 0 }
                ),
            "layout poses are finite with positive scale"
        );
    }

    private static bool IsFinite(Float3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static void Assert(bool condition, string name)
    {
        if (!condition)
            throw new Exception("Runeforge shop layout: " + name);
    }
}
