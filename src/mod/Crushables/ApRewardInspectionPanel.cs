using HarmonyLib;
using Il2CppSG.Claymore.Interaction;
using Il2CppSG.Claymore.ResourceLoading;
using Il2CppSG.Claymore.UI.Inspection;
using UnityEngine;

namespace UntilYouFallArchipelago;

internal static class ApRewardInspectionPanel
{
    private const string LargeKeyName = "HandUI_NameAndDescription_LargeKey";
    private static ResourceRequestKey? _largeKey;
    private static readonly Dictionary<
        int,
        (
            DescriptionInspectionPanel Panel,
            RectTransform Canvas,
            RectTransform Body,
            Vector2 CanvasPosition,
            Vector2 BodySize,
            Vector2 BodyPosition,
            Action<string> Log
        )
    > OriginalLayouts = [];

    public static void Install(HarmonyLib.Harmony harmony) => harmony.PatchAll(typeof(ApRewardInspectionPanel));

    [HarmonyPatch(typeof(DescriptionInspectionPanel), nameof(DescriptionInspectionPanel.Populate))]
    [HarmonyPrefix]
    public static void RestoreBeforePopulate(DescriptionInspectionPanel __instance)
    {
        if (!OriginalLayouts.Remove(__instance.GetInstanceID(), out var original))
            return;
        if (original.Panel == null || original.Canvas == null || original.Body == null)
            return;

        original.Canvas.anchoredPosition = original.CanvasPosition;
        original.Body.sizeDelta = original.BodySize;
        original.Body.anchoredPosition = original.BodyPosition;
        original.Log.Write(
            "APInspection",
            $"Original layout restored panel={__instance.GetInstanceID()} canvasPosition={original.CanvasPosition} size={original.BodySize} position={original.BodyPosition}"
        );
    }

    public static void UseLargePanel(RewardInteractable reward, Action<string> log, string feature)
    {
        if (_largeKey == null)
            _largeKey = Resources
                .FindObjectsOfTypeAll<ResourceRequestKey>()
                .FirstOrDefault(candidate => candidate != null && candidate.name == LargeKeyName);
        var key = _largeKey;
        if (key == null)
        {
            log.Write(feature, "Large inspection panel failed");
            return;
        }
        reward.inspectionPanelKey = key;
        log.Write(feature, $"Large inspection panel selected key='{key.name}'");
    }

    public static void Compact(DescriptionInspectionPanel panel, Action<string> log, string feature)
    {
        var canvas = panel.transform.Find("Pivot/Canvas")?.GetComponent<RectTransform>();
        var body = canvas?.Find("Body")?.GetComponent<RectTransform>();
        if (canvas == null || body == null)
        {
            log.Write(feature, "Inspection layout unavailable path='Pivot/Canvas/Body'");
            return;
        }

        foreach (
            var stale in OriginalLayouts.Where(entry => entry.Value.Panel == null).Select(entry => entry.Key).ToArray()
        )
            OriginalLayouts.Remove(stale);
        if (
            OriginalLayouts.TryAdd(
                panel.GetInstanceID(),
                (panel, canvas, body, canvas.anchoredPosition, body.sizeDelta, body.anchoredPosition, log)
            )
        )
            log.Write(
                "APInspection",
                $"Original layout captured panel={panel.GetInstanceID()} canvasPosition={canvas.anchoredPosition} size={body.sizeDelta} position={body.anchoredPosition}"
            );

        var canvasPosition = canvas.anchoredPosition;
        canvasPosition.y = 0f;
        canvas.anchoredPosition = canvasPosition;
        var size = body.sizeDelta;
        size.x = RuneforgeShopRules.InspectionPanelWidth;
        size.y = RuneforgeShopRules.InspectionPanelHeight;
        body.sizeDelta = size;
        var position = body.anchoredPosition;
        position.y = RuneforgeShopRules.InspectionPanelOffsetY;
        body.anchoredPosition = position;
        LogLayout(canvas, log, feature);

        log.Write(
            feature,
            $"Inspection layout compacted size={body.sizeDelta} position={body.anchoredPosition} panel='{panel.name}' panelId={panel.GetInstanceID()} canvasPosition={canvas.anchoredPosition}"
        );
    }

    private static void LogLayout(RectTransform canvas, Action<string> log, string feature)
    {
        foreach (var rect in canvas.GetComponentsInChildren<RectTransform>(true))
            log.Write(
                "APInspectionLayout",
                $"{feature} path={rect.name}<{rect.parent?.name} active={rect.gameObject.activeSelf} size={rect.sizeDelta} pos={rect.anchoredPosition} anchors={rect.anchorMin}-{rect.anchorMax} pivot={rect.pivot} rect={rect.rect.size}"
            );
    }
}
