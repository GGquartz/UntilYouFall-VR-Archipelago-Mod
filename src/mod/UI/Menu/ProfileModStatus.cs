using HarmonyLib;
using Il2CppSG.Claymore.UI;
using Il2CppTMPro;
using UnityEngine;

namespace UntilYouFallArchipelago;

internal sealed class ProfileModStatus : ConnectionStatusLabel
{
    private const string Category = "APMenu";
    private const float FontSize = 110f;
    private static readonly Vector2 LabelPosition = new(0f, -825f);
    private static readonly Vector2 LabelSize = new(1800f, 180f);
    private static ProfileModStatus? _instance;

    public ProfileModStatus(HarmonyLib.Harmony harmony, Func<bool> isConnected, Action<string> log)
        : base(isConnected, log, Category, "Archipelago Mod Status")
    {
        _instance = this;
        var onEnable = AccessTools.Method(typeof(ProfileUI), "OnEnable");
        if (onEnable == null)
        {
            log.Write(Category, "ProfileUI.OnEnable was not found");
            return;
        }

        harmony.Patch(onEnable, postfix: new HarmonyMethod(typeof(ProfileModStatus), nameof(Show)));
        harmony.Patch(
            AccessTools.Method(typeof(ProfileUI), nameof(ProfileUI.AttemptLoad)),
            postfix: new HarmonyMethod(typeof(ProfileModStatus), nameof(Show))
        );

        log.Write(Category, "Profile selection mod status enabled");
    }

    private static void Show(ProfileUI __instance) => _instance?.ShowLabel(() => _instance.Create(__instance));

    private TextMeshProUGUI? Create(ProfileUI profileUi)
    {
        var source =
            profileUi._newGameTextMeshPro
            ?? profileUi
                .GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(candidate => candidate != null && candidate.font != null);
        if (source == null)
        {
            Log("Profile selection has no loaded TMP font");
            return null;
        }

        var parent = profileUi.transform.parent ?? source.rectTransform.parent ?? profileUi.transform;
        var text = CreateText(parent, source, FontSize, out var rect);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = rect.anchorMin;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        rect.SetAsLastSibling();
        rect.anchoredPosition = LabelPosition;
        rect.sizeDelta = LabelSize;

        LogCreated(source, parent, rect, text);
        return text;
    }
}
