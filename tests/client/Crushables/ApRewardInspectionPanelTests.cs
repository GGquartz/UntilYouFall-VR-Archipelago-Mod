using Il2CppSG.Claymore.UI.Inspection;
using UnityEngine;
using UntilYouFallArchipelago;

internal static class ApRewardInspectionPanelTests
{
    public static void Run()
    {
        var panel = new DescriptionInspectionPanel();
        var canvas = new RectTransform { anchoredPosition = new Vector2(12f, 0.2f) };
        var body = new RectTransform { anchoredPosition = new Vector2(17f, 200f), sizeDelta = new Vector2(650f, 595f) };
        panel.transform.Children.Add("Pivot/Canvas", canvas);
        canvas.Children.Add("Body", body);
        var otherPanel = new DescriptionInspectionPanel();

        ApRewardInspectionPanel.RestoreBeforePopulate(panel);
        Assert(body.anchoredPosition.y == 200f, "untouched vanilla panel remains unchanged");
        ApRewardInspectionPanel.Compact(panel, _ => { }, "test");
        ApRewardInspectionPanel.Compact(panel, _ => { }, "test");
        Assert(
            canvas.anchoredPosition.y == 0f && body.sizeDelta.y == 150f && body.anchoredPosition.y == 0f,
            "AP compacts the actual cached panel"
        );
        ApRewardInspectionPanel.RestoreBeforePopulate(otherPanel);
        Assert(body.sizeDelta.y == 150f, "another hand does not restore this panel");
        ApRewardInspectionPanel.RestoreBeforePopulate(panel);
        Assert(
            canvas.anchoredPosition.x == 12f
                && canvas.anchoredPosition.y == 0.2f
                && body.anchoredPosition.x == 17f
                && body.anchoredPosition.y == 200f
                && body.sizeDelta.x == 650f
                && body.sizeDelta.y == 595f,
            "vanilla Populate gets the exact original layout even after repeated AP compaction"
        );
        ApRewardInspectionPanel.Compact(panel, _ => { }, "test");
        Assert(body.sizeDelta.y == 150f, "AP can reuse the restored vanilla instance");
        ApRewardInspectionPanel.RestoreBeforePopulate(panel);
        body.anchoredPosition = new Vector2(21f, 210f);
        ApRewardInspectionPanel.Compact(panel, _ => { }, "test");
        ApRewardInspectionPanel.RestoreBeforePopulate(panel);
        Assert(body.anchoredPosition.y == 210f, "a later vanilla layout is captured afresh");
        Console.WriteLine("PASS: cached inspection layout vanilla/AP/vanilla/AP restoration and hand isolation");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
