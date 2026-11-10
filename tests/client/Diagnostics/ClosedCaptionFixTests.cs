using UntilYouFallArchipelago;

internal static class ClosedCaptionFixTests
{
    public static void Run()
    {
        Assert(
            ClosedCaptionFixRules.ShouldEnableDrawEffect(true, true, 42),
            "visible banter uses the requested post effect"
        );
        Assert(
            !ClosedCaptionFixRules.ShouldEnableDrawEffect(true, true, 0),
            "cleared text disables drawing even before the manager removes the caption"
        );
        for (var refresh = 0; refresh < 3; refresh++)
            Assert(
                !ClosedCaptionFixRules.ShouldEnableDrawEffect(true, true, 0),
                "repeated native queue requests cannot redraw the empty background"
            );
        Assert(
            ClosedCaptionFixRules.ShouldEnableDrawEffect(true, true, 12),
            "the next caption restores drawing and remaining fade text is not cut off"
        );
        Assert(
            !ClosedCaptionFixRules.ShouldEnableDrawEffect(false, true, 12),
            "caption text never overrides the native disabled after-fade queue"
        );
        Assert(
            !ClosedCaptionFixRules.ShouldEnableDrawEffect(true, false, 12),
            "an inactive UI never restores its effect"
        );
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
