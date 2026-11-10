using HarmonyLib;
using Il2CppSG.Claymore.Armaments;
using Il2CppSG.Claymore.Armory;
using Il2CppSG.Claymore.Interaction;
using Il2CppSG.Claymore.Player;
using Il2CppSG.Claymore.SaveSystem;
using Il2CppSG.GlobalEvents;
using Il2CppTMPro;
using UnityEngine;

namespace UntilYouFallArchipelago;

internal sealed class WeaponUpgradeCap
{
    private static WeaponUpgradeCap _instance = null!;
    private readonly LogChannel _log;
    private readonly Action<ApNotice> _notifyTechnical;
    private TextMeshPro? _altarLabel;
    private long _nextAltarSearchAt;
    private float _altarBaseFontSize;
    private int _altarBaseLength;
    private int? _itemCount;
    private int _blockedCrushDepth;

    public WeaponUpgradeCap(HarmonyLib.Harmony harmony, Action<string> log, Action<ApNotice> notifyTechnical)
    {
        _instance = this;
        _log = log.Channel("APWeaponUpgradeCap");
        _notifyTechnical = notifyTechnical;
        harmony.PatchAll(typeof(WeaponUpgradeCap));
    }

    public void Update(int? itemCount, bool canHaveAltar)
    {
        if (_itemCount != itemCount)
            _itemCount = itemCount;
        if (itemCount.HasValue)
            UpdateAltarLabel(itemCount.Value, canHaveAltar);
    }

    [HarmonyPatch(typeof(ArmamentInventory), nameof(ArmamentInventory.BuyArmament), typeof(ArmamentID), typeof(int))]
    [HarmonyPrefix]
    private static bool AllowPurchase(ArmamentID __0, int __1, ref bool __result)
    {
        if (!WeaponUpgradeCapRules.IsBlockedUpgrade(__1, _instance._itemCount))
            return true;

        __result = false;
        _instance.Notify();
        return false;
    }

    [HarmonyPatch(typeof(ArmamentInventory), nameof(ArmamentInventory.LevelUp), typeof(PlayerHandState))]
    [HarmonyPrefix]
    private static bool AllowLevelUp(ArmamentInventory __instance, PlayerHandState __0)
    {
        if (_instance._itemCount == null)
            return true;
        var targetLevel = __instance.GetLevel(__0.Armament) + 1;
        if (!WeaponUpgradeCapRules.IsBlockedUpgrade(targetLevel, _instance._itemCount))
            return true;

        _instance.Notify();
        return false;
    }

    [HarmonyPatch(typeof(PurchaseInteractable), nameof(PurchaseInteractable.CanCrush), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool AllowPurchaseCrush(PurchaseInteractable __instance, ref bool __result)
    {
        if (!WeaponUpgradeCapRules.IsBlockedUpgrade(__instance.ArmamentLevel, _instance._itemCount))
            return true;

        __result = false;
        return false;
    }

    [HarmonyPatch(typeof(ArmamentID), nameof(ArmamentID.MaxUpgradeLevel), MethodType.Getter)]
    [HarmonyPostfix]
    private static void CapMaxUpgradeLevel(ref int __result)
    {
        __result = WeaponUpgradeCapRules.CapNativeMaxUpgradeLevel(__result, _instance._itemCount);
    }

    [HarmonyPatch(typeof(UpgradeInteractable), nameof(UpgradeInteractable.HasNextLevel))]
    [HarmonyPostfix]
    private static void BlockUpgradeHasNextLevel(UpgradeInteractable __instance, ref bool __result)
    {
        if (!__result || _instance._itemCount == null)
            return;

        if (WeaponUpgradeCapRules.IsBlockedUpgrade(__instance.GetNextLevel(), _instance._itemCount))
            __result = false;
    }

    [HarmonyPatch(typeof(CrushInteractable), nameof(CrushInteractable.FinishCrush))]
    [HarmonyPrefix]
    private static void BeginFinishCrush(CrushInteractable __instance, out bool __state)
    {
        var purchase = __instance?.TryCast<PurchaseInteractable>();
        __state =
            purchase != null && WeaponUpgradeCapRules.IsBlockedUpgrade(purchase.ArmamentLevel, _instance._itemCount);
        if (__state)
            _instance._blockedCrushDepth++;
    }

    [HarmonyPatch(typeof(CrushInteractable), nameof(CrushInteractable.FinishCrush))]
    [HarmonyFinalizer]
    private static Exception? EndFinishCrush(Exception? __exception, bool __state)
    {
        if (__state)
            _instance._blockedCrushDepth--;

        return __exception;
    }

    [HarmonyPatch(typeof(GlobalEvent), nameof(GlobalEvent.Raise), new Type[0])]
    [HarmonyPrefix]
    private static bool AllowGlobalEvent(GlobalEvent __instance)
    {
        if (_instance._blockedCrushDepth <= 0)
            return true;

        _instance._log.Write($"Stop blocked crush event event={__instance?.name ?? "<null>"}");
        return false;
    }

    private void UpdateAltarLabel(int itemCount, bool canHaveAltar)
    {
        if (_altarLabel == null && canHaveAltar && Environment.TickCount64 >= _nextAltarSearchAt)
        {
            _nextAltarSearchAt = Environment.TickCount64 + 3000;
            _altarLabel = Resources.FindObjectsOfTypeAll<TextMeshPro>().FirstOrDefault(IsUpgradeAltarLabel);
            if (_altarLabel != null)
            {
                _altarBaseFontSize = _altarLabel.fontSize;
                _altarBaseLength = Math.Max(_altarLabel.text?.Length ?? 0, 1);
                _log.Write("Found upgrade label at " + HierarchyPath(_altarLabel.transform));
            }
        }

        if (_altarLabel == null)
            return;
        var desiredText = WeaponUpgradeCapRules.AltarLabel(itemCount);
        if (_altarLabel.text == desiredText)
            return;

        var previousText = _altarLabel.text;
        _altarLabel.text = desiredText;
        var scaledFontSize =
            _altarBaseFontSize * _altarBaseLength / Math.Max(desiredText.Length, _altarBaseLength) * 1.3f;
        if (!Mathf.Approximately(_altarLabel.fontSize, scaledFontSize))
            _altarLabel.fontSize = scaledFontSize;
        _altarLabel.ForceMeshUpdate();

        _log.Write($"Upgrade label changed from={previousText} to={desiredText}");
    }

    private static bool IsUpgradeAltarLabel(TextMeshPro text)
    {
        var transform = text.transform;
        return text.gameObject.activeInHierarchy
            && WeaponUpgradeCapRules.IsUpgradeAltarHierarchy(
                transform.name,
                transform.parent?.name,
                transform.parent?.parent?.name,
                transform.parent?.parent?.parent?.name
            );
    }

    private static string HierarchyPath(Transform transform)
    {
        var names = new List<string>();
        for (var current = transform; current != null; current = current.parent)
            names.Add(current.name);
        names.Reverse();

        return string.Join("/", names);
    }

    private void Notify()
    {
        var message = WeaponUpgradeCapRules.Message(_itemCount ?? 0);
        _log.Write($"Blocked upgrade items={_itemCount} message={message}");
        _notifyTechnical(ApNotice.Error(message));
    }
}
