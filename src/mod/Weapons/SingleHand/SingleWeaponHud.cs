using HarmonyLib;
using Il2CppSG.Claymore.Armaments;
using Il2CppSG.Claymore.UI;

namespace UntilYouFallArchipelago;

internal sealed class SingleWeaponHud
{
    private static SingleWeaponHud _instance = null!;
    private readonly Func<bool> _active;
    private readonly Func<string?, bool> _abilityLocked;
    private readonly LogChannel _log;
    private bool _logged;

    public SingleWeaponHud(
        HarmonyLib.Harmony harmony,
        Func<bool> active,
        Func<string?, bool> abilityLocked,
        Action<string> log
    )
    {
        _instance = this;
        _active = active;
        _abilityLocked = abilityLocked;
        _log = log.Channel("APWeaponUI");
        harmony.PatchAll(typeof(SingleWeaponHud));
    }

    private static bool Hidden(AbilityChargeUINoCanvas ui)
    {
        if (!_instance._active())
            return false;

        var hand = ui._handState?.GetState();
        return WeaponUiRules.HideSuper(
            true,
            hand != null,
            hand?.Armament != null,
            _instance._abilityLocked(hand?.Armament?.GUID)
        );
    }

    [HarmonyPatch(typeof(AbilityChargeUINoCanvas), nameof(AbilityChargeUINoCanvas.Update))]
    [HarmonyPrefix]
    private static bool UpdateSuper(AbilityChargeUINoCanvas __instance)
    {
        if (!Hidden(__instance))
            return true;

        Clear(__instance);
        return false;
    }

    [HarmonyPatch(typeof(AbilityChargeUINoCanvas), nameof(AbilityChargeUINoCanvas.SetTarget))]
    [HarmonyPrefix]
    private static bool SetTarget(AbilityChargeUINoCanvas __instance, Armament __0)
    {
        if (!_instance._active())
            return true;
        if (__0 != null && !_instance._abilityLocked(__0.ID?.GUID))
            return true;

        Clear(__instance);
        return false;
    }

    [HarmonyPatch(typeof(AbilityChargeUINoCanvas), nameof(AbilityChargeUINoCanvas.SetAlpha))]
    [HarmonyPrefix]
    private static void SetAlpha(AbilityChargeUINoCanvas __instance, ref float __0)
    {
        if (Hidden(__instance))
            __0 = 0;
    }

    private static void Clear(AbilityChargeUINoCanvas ui)
    {
        ui.Unregister();
        ui._registered = false;
        ui._target = null!;
        ui._targetDef = null!;
        ui._isCharged = false;
        ui.SetAlpha(0);
        if (!_instance._logged)
        {
            _instance._logged = true;
            _instance._log.Write("Cleared super target for an empty hand");
        }
    }
}
