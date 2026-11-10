using HarmonyLib;
using Il2CppSG.ProtoFramework.ClosedCaption;
using Il2CppTMPro;
using UnityEngine;

namespace UntilYouFallArchipelago;

/// Fix bug where closed captions were showing their background after the text is gone
internal sealed class ClosedCaptionFix
{
    private static ClosedCaptionFix _instance = null!;
    private readonly LogChannel _log;

    public ClosedCaptionFix(HarmonyLib.Harmony harmony, Action<string> log)
    {
        _instance = this;
        _log = log.Channel("APCaptionFix");
        harmony.PatchAll(typeof(ClosedCaptionFix));
    }

    [HarmonyPatch(typeof(ClosedCaptionUIHQ), nameof(ClosedCaptionUIHQ.UpdateDrawEffect))]
    [HarmonyPrefix]
    private static bool GuardDrawEffect(ClosedCaptionUIHQ __instance)
    {
        if (__instance.isActiveAndEnabled && VisibleCharacters(__instance) > 0)
            return true;

        SetDrawEffect(__instance, false);
        return false;
    }

    [HarmonyPatch(typeof(ClosedCaptionUI), nameof(ClosedCaptionUI.Display))]
    [HarmonyPatch(typeof(ClosedCaptionUI), nameof(ClosedCaptionUI.Disappear))]
    [HarmonyPatch(typeof(ClosedCaptionUI), nameof(ClosedCaptionUI.UpdateDisplay))]
    [HarmonyPostfix]
    private static void ApplyCaptionRender(ClosedCaptionUI __instance)
    {
        var characters = VisibleCharacters(__instance);
        if (__instance.isActiveAndEnabled && characters > 0)
            ShowRoot(__instance);
        var requested = __instance._effectInQueue;
        if (requested == null)
            return;

        SetDrawEffect(
            __instance,
            ClosedCaptionFixRules.ShouldEnableDrawEffect(requested.Value, __instance.isActiveAndEnabled, characters)
        );
    }

    private static void SetDrawEffect(ClosedCaptionUI ui, bool enabled)
    {
        var effect = ui._effect;
        if (effect == null || effect.enabled == enabled)
            return;

        effect.enabled = enabled;
        _instance._log.Write(
            $"Draw effect {(enabled ? "enabled" : "disabled")} ui={ui.GetInstanceID()} effect={effect.GetInstanceID()} chars={VisibleCharacters(ui)}"
        );
    }

    private static void ShowRoot(ClosedCaptionUI ui)
    {
        var root = ui._root?.gameObject;
        if (root == null || root.activeSelf)
            return;

        root.SetActive(true);
        _instance._log.Write($"Root restored for caption ui={ui.GetInstanceID()}");
    }

    private static int VisibleCharacters(ClosedCaptionUI ui)
    {
        var root = ui._root;
        if (root == null)
            return 0;

        var characters = 0;
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            characters += text?.text?.Length ?? 0;
        foreach (var text in root.GetComponentsInChildren<TextMesh>(true))
            characters += text?.text?.Length ?? 0;

        return characters;
    }
}
