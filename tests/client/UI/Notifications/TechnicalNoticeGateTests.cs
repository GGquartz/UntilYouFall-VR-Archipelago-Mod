using UntilYouFallArchipelago;

internal static class TechnicalNoticeGateTests
{
    public static void Run()
    {
        var gate = new TechnicalNoticeGate();
        Assert(gate.TryReserve(), "the first technical notice is accepted");
        Assert(
            !gate.TryReserve() && !gate.TryReserve(),
            "repeated notices are discarded while one is pending or visible"
        );
        gate.Release();
        Assert(gate.TryReserve(), "a new notice is accepted after the previous display completes");
        Assert(NotificationPanelLayout.ApVerticalPosition == -0.25f, "the existing AP item position stays unchanged");
        Assert(
            NotificationPanelLayout.TechnicalVerticalPosition < NotificationPanelLayout.ApVerticalPosition - 0.10f,
            "the technical notice is placed below the AP item notice without overlap"
        );
        Assert(
            NotificationPanelLayout.PanelWidth == 1000f && NotificationPanelLayout.HorizontalPadding == 60f,
            "the Forge skin keeps the proven notification width and wrapping padding"
        );
        Assert(NotificationPanelLayout.HeightFor(40f, false) == 70f, "a short notice keeps the compact minimum height");
        Assert(
            NotificationPanelLayout.HeightFor(130f, false) == 150f,
            "wrapped text grows the Forge skin instead of overflowing it"
        );
        Assert(
            NotificationPanelLayout.HeightFor(60f, true) == 120f,
            "location notices keep their larger minimum height"
        );
        Assert(
            NotificationPanelLayout.VerticalOffsetFor(6f) == -6f
                && NotificationPanelLayout.VerticalOffsetFor(-4f) == 4f,
            "visible glyph bounds are centered independently of font metrics"
        );
        Assert(
            NotificationPanelLayout.NotificationResource == "UI/CashOut_View_HQ",
            "the Forge skin loads from the resource that owns the panel instead of depending on scene state"
        );
        Console.WriteLine("PASS: technical notice single-slot deduplication and separate lower panel layout");
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok)
            throw new Exception("Technical notice gate: " + message);
    }
}
