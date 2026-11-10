using HarmonyLib;
using Il2CppSG.Claymore.Armaments;
using Il2CppSG.Claymore.Interaction;
using Il2CppSG.Claymore.UI.Inspection;
using UnityEngine;

namespace UntilYouFallArchipelago;

internal sealed class WeaponPurchaseUi
{
    private static WeaponPurchaseUi _instance = null!;
    private readonly Func<ArmamentID?, bool> _locked;
    private readonly Dictionary<IntPtr, bool> _hiddenRoots = [];

    public WeaponPurchaseUi(HarmonyLib.Harmony harmony, Func<ArmamentID?, bool> locked, Action<string> log)
    {
        _instance = this;
        _locked = locked;
        harmony.PatchAll(typeof(WeaponPurchaseUi));
    }

    private static bool LockedPurchase(CrushInteractable? purchase)
    {
        var weapon = purchase == null ? null : purchase.TryCast<PurchaseInteractable>();
        return weapon != null && _instance._locked(weapon.ArmamentID);
    }

    [HarmonyPatch(typeof(TitleUIPanel), "Activate")]
    [HarmonyPatch(typeof(TitleUIPanel), "RefreshCanAfford")]
    [HarmonyPostfix]
    private static void RefreshTitle(TitleUIPanel __instance)
    {
        HideRoot(__instance.purchaseRoot, __instance.currentPurchase);
    }

    [HarmonyPatch(typeof(TitleUIPanelLQ), "Activate")]
    [HarmonyPatch(typeof(TitleUIPanelLQ), "RefreshCanAfford")]
    [HarmonyPostfix]
    private static void RefreshTitleLq(TitleUIPanelLQ __instance)
    {
        HideRoot(__instance.purchaseRoot, __instance.currentPurchase);
    }

    [HarmonyPatch(typeof(TitleUIPanel), "Activate")]
    [HarmonyPrefix]
    private static void RestoreTitle(TitleUIPanel __instance) => RestoreRoot(__instance.purchaseRoot);

    [HarmonyPatch(typeof(TitleUIPanelLQ), "Activate")]
    [HarmonyPrefix]
    private static void RestoreTitleLq(TitleUIPanelLQ __instance) => RestoreRoot(__instance.purchaseRoot);

    private static void HideRoot(GameObject? root, CrushInteractable? purchase)
    {
        if (root == null)
            return;
        if (!LockedPurchase(purchase))
        {
            RestoreRoot(root);
            return;
        }

        _instance._hiddenRoots.TryAdd(root.Pointer, root.activeSelf);
        root.SetActive(false);
    }

    private static void RestoreRoot(GameObject? root)
    {
        if (root != null && _instance._hiddenRoots.Remove(root.Pointer, out var active))
            root.SetActive(active);
    }

    [HarmonyPatch(typeof(TitleUIPanelLQTextMesh), "Activate")]
    [HarmonyPatch(typeof(TitleUIPanelLQTextMesh), "RefreshCanAfford")]
    [HarmonyPostfix]
    private static void RefreshTitleTextMesh(TitleUIPanelLQTextMesh __instance)
    {
        if (!LockedPurchase(__instance.currentPurchase))
            return;

        foreach (
            var renderer in new[]
            {
                __instance.costTextRenderer,
                __instance.currentTextRenderer,
                __instance.maxTextRenderer,
                __instance.purchaseInstructionsTextRenderer,
                __instance.upgradeInstructionsTextRenderer,
                __instance.cantAffordTextRenderer,
                __instance.maxedOutTextRenderer,
            }
        )
            if (renderer != null)
                renderer.enabled = false;
    }
}
