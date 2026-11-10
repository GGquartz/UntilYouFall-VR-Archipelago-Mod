using HarmonyLib;
using Il2CppSG.Claymore.Armaments;
using Il2CppSG.Claymore.Armaments.Traits;
using Il2CppSG.Claymore.Interaction;
using Il2CppSG.Claymore.UI.Inspection;
using Il2CppSG.GlobalEvents.Variables;

namespace UntilYouFallArchipelago;

internal sealed class ExtraTraitSlots
{
    private static ExtraTraitSlots _instance = null!;

    [ThreadStatic]
    private static IntVariable? _socketVariable;
    private readonly LogChannel _log;
    private HashSet<long> _unlocks = [];

    public ExtraTraitSlots(HarmonyLib.Harmony harmony, Action<string> log)
    {
        _instance = this;
        _log = log.Channel("APTraits");
        harmony.PatchAll(typeof(ExtraTraitSlots));
    }

    public void Update(IReadOnlyCollection<long>? unlocks)
    {
        if (unlocks == null || _unlocks.SetEquals(unlocks))
            return;

        _unlocks = [.. unlocks];
        _log.Write($"Trait slot unlocks refreshed count={_unlocks.Count}");

        foreach (var panel in UnityEngine.Object.FindObjectsOfType<InspectionPanel>())
        {
            if (!panel.IsActive || panel.activeHand == null || panel.crushInteractableSource == null)
                continue;
            try
            {
                panel.Populate(panel.activeHand, panel.crushInteractableSource);
            }
            catch (Exception ex)
            {
                _log.Write("Active inspection refresh failed: " + ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(Armament), nameof(Armament.UpgradeSockets), MethodType.Getter)]
    [HarmonyPatch(typeof(Armament), nameof(Armament.AvailableSockets), MethodType.Getter)]
    [HarmonyPostfix]
    private static void AddExtraSocket(Armament __instance, ref int __result)
    {
        if (_socketVariable != null && _socketVariable.Pointer == __instance.InstancedVariables.TraitSockets.Pointer)
            return;

        var weapon = WeaponCatalog.ByGuid(__instance.ID?.GUID ?? "");
        if (weapon != null)
            __result = ExtraTraitSlotRules.Apply(__result, weapon.Id, _instance._unlocks);
    }

    [HarmonyPatch(typeof(TraitDisplayPanel), nameof(TraitDisplayPanel.Populate))]
    [HarmonyPatch(typeof(TraitDisplayPanelLQ), nameof(TraitDisplayPanel.Populate))]
    [HarmonyPatch(typeof(TraitDisplayPanelLQTextMesh), nameof(TraitDisplayPanel.Populate))]
    [HarmonyPrefix]
    private static void BeginInspectionSocket(ArmamentID __0, out IntVariable? __state)
    {
        __state = _socketVariable;
        SetSocketVariable(__0?.CurrentInstance);
    }

    [HarmonyPatch(typeof(BaseArmamentTrait), "CanBeUsed")]
    [HarmonyPatch(typeof(StatModArmamentTrait), "CanBeUsed")]
    [HarmonyPatch(typeof(UpgradeSuperTrait), "CanBeUsed")]
    [HarmonyPrefix]
    private static void BeginTraitSocket(PlayerHand __0, out IntVariable? __state)
    {
        __state = _socketVariable;
        SetSocketVariable(__0?.BoundArmament);
    }

    private static void SetSocketVariable(Armament? armament)
    {
        _socketVariable = null;
        var weapon = WeaponCatalog.ByGuid(armament?.ID?.GUID ?? "");
        if (weapon == null || ExtraTraitSlotRules.Apply(0, weapon.Id, _instance._unlocks) == 0)
            return;

        _socketVariable = armament?.InstancedVariables?.TraitSockets;
    }

    [HarmonyPatch(typeof(IntVariable), nameof(IntVariable.Value), MethodType.Getter)]
    [HarmonyPostfix]
    private static void AddSocketValue(IntVariable __instance, ref int __result)
    {
        if (_socketVariable != null && __instance.Pointer == _socketVariable.Pointer)
            __result += 1;
    }

    [HarmonyPatch(typeof(TraitDisplayPanel), nameof(TraitDisplayPanel.Populate))]
    [HarmonyPatch(typeof(TraitDisplayPanelLQ), nameof(TraitDisplayPanel.Populate))]
    [HarmonyPatch(typeof(TraitDisplayPanelLQTextMesh), nameof(TraitDisplayPanel.Populate))]
    [HarmonyFinalizer]
    private static Exception? EndInspectionSocket(Exception? __exception, IntVariable? __state)
    {
        var scoped = _socketVariable != null;
        _socketVariable = __state;
        if (!scoped || __exception == null || !__exception.Message.Contains("cache list for this list panel"))
            return __exception;

        _instance._log.Write("Inspection trait icons exceed the panel cache");
        return null;
    }

    [HarmonyPatch(typeof(BaseArmamentTrait), "CanBeUsed")]
    [HarmonyPatch(typeof(StatModArmamentTrait), "CanBeUsed")]
    [HarmonyPatch(typeof(UpgradeSuperTrait), "CanBeUsed")]
    [HarmonyFinalizer]
    private static Exception? EndSocket(Exception? __exception, IntVariable? __state)
    {
        _socketVariable = __state;
        return __exception;
    }
}
