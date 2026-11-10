using HarmonyLib;
using Il2CppSG.Claymore.UI;
using Il2CppTMPro;
using UnityEngine;

namespace UntilYouFallArchipelago;

internal sealed class PauseModStatus : ConnectionStatusLabel
{
    private const string Category = "APPause";
    private const float FontSize = 42f;
    private const float OffsetBelowResume = -90f;
    private static readonly Vector2 LabelSize = new(1200f, 90f);
    private static PauseModStatus? _instance;

    public PauseModStatus(HarmonyLib.Harmony harmony, Func<bool> isConnected, Action<string> log)
        : base(isConnected, log, Category, "Archipelago Pause Status")
    {
        _instance = this;
        var onEnable = AccessTools.Method(typeof(PauseScreen), "OnEnable");
        if (onEnable == null)
        {
            log.Write(Category, "PauseScreen.OnEnable was not found");
            return;
        }

        harmony.Patch(onEnable, postfix: new HarmonyMethod(typeof(PauseModStatus), nameof(Show)));
        log.Write(Category, "Pause menu mod status enabled");
    }

    private static void Show(PauseScreen __instance) => _instance?.ShowLabel(() => _instance.Create(__instance));

    private TextMeshProUGUI? Create(PauseScreen pause)
    {
        var resume = pause.resumeButton;
        var source = resume
            ?.GetComponentsInChildren<TextMeshProUGUI>(true)
            .FirstOrDefault(candidate => candidate != null && candidate.font != null);
        var parent = resume?.transform.parent;
        if (resume == null || source == null || parent == null)
        {
            Log("Resume button has no loaded TMP font or parent");
            return null;
        }

        var text = CreateText(parent, source, FontSize, out var rect);
        var resumeRect = resume.GetComponent<RectTransform>();
        rect.anchorMin = resumeRect.anchorMin;
        rect.anchorMax = resumeRect.anchorMax;
        rect.pivot = resumeRect.pivot;
        rect.anchoredPosition = resumeRect.anchoredPosition + new Vector2(0f, OffsetBelowResume);
        rect.sizeDelta = LabelSize;
        rect.localRotation = resumeRect.localRotation;
        rect.localScale = resumeRect.localScale;
        rect.SetSiblingIndex(resume.transform.GetSiblingIndex() + 1);

        LogCreated(source, parent, rect, text);
        return text;
    }
}
