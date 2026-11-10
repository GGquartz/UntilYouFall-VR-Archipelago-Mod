using HarmonyLib;
using Il2CppSG.Claymore.Armaments;
using Il2CppSG.Claymore.Armory;
using Il2CppSG.Claymore.Interaction;
using UnityEngine.Events;

namespace UntilYouFallArchipelago;

internal sealed class WeaponHintRunestones
{
    private static WeaponHintRunestones _instance = null!;
    private readonly Func<ArmamentID?, bool> _locked;
    private readonly Func<ArchipelagoClient?> _client;
    private readonly LogChannel _log;
    private readonly HashSet<IntPtr> _finishing = [];

    private sealed record FinishState(UnityEvent OnCrushed, bool RematOnCrush, bool SummonOnCrush);

    public WeaponHintRunestones(
        HarmonyLib.Harmony harmony,
        Func<ArmamentID?, bool> locked,
        Func<ArchipelagoClient?> client,
        Action<string> log
    )
    {
        _instance = this;
        _locked = locked;
        _client = client;
        _log = log.Channel("APWeaponHint");
        harmony.Patch(
            AccessTools.Method(typeof(CrushInteractable), nameof(CrushInteractable.FinishCrush)),
            prefix: new HarmonyMethod(typeof(WeaponHintRunestones), nameof(BeforeFinish)),
            postfix: new HarmonyMethod(typeof(WeaponHintRunestones), nameof(AfterFinish)),
            finalizer: new HarmonyMethod(typeof(WeaponHintRunestones), nameof(EndFinish))
        );
        harmony.Patch(
            AccessTools.Method(typeof(PurchaseInteractable), nameof(PurchaseInteractable.OnCrushComplete)),
            prefix: new HarmonyMethod(typeof(WeaponHintRunestones), nameof(Complete))
        );
        harmony.Patch(
            AccessTools.Method(typeof(WeaponRackSlot), nameof(WeaponRackSlot.Buy)),
            prefix: new HarmonyMethod(typeof(WeaponHintRunestones), nameof(AllowBuy))
        );
    }

    private static bool AllowBuy(WeaponRackSlot __instance) => !_instance._locked(__instance.ArmamentID);

    private static bool BeforeFinish(CrushInteractable __instance, out FinishState? __state)
    {
        __state = null;
        var purchase = __instance.TryCast<PurchaseInteractable>();
        if (purchase == null || !_instance._locked(purchase.ArmamentID))
            return true;

        __state = new FinishState(__instance.onCrushed, __instance.rematOnCrush, __instance.summonOnCrush);
        _instance._finishing.Add(__instance.Pointer);
        __instance.onCrushed = new UnityEvent();
        __instance.rematOnCrush = true;
        __instance.summonOnCrush = true;

        return true;
    }

    private static bool Complete(PurchaseInteractable __instance)
    {
        if (!_instance._locked(__instance.ArmamentID))
            return true;

        __instance.EndShake();
        __instance.crushRoutine = null;
        __instance.crushAmount = 0;

        _instance._log.Write(
            $"Native purchase completion stopped scoped={_instance._finishing.Contains(__instance.Pointer)}"
        );
        return false;
    }

    private static void AfterFinish(CrushInteractable __instance, FinishState? __state)
    {
        if (__state == null)
            return;

        if (__instance.rematCoroutine != null)
            __instance.StopCoroutine(__instance.rematCoroutine);
        __instance.rematCoroutine = __instance.StartCoroutine(__instance.DelayedRemat(false));

        var sent = _instance._client()?.HintNextWeapon() == true;
        _instance._log.Write($"Crush finished requestSent={sent} native remat started");
    }

    private static Exception? EndFinish(CrushInteractable __instance, FinishState? __state, Exception? __exception)
    {
        if (__state == null)
            return __exception;
        try
        {
            if (__instance != null)
            {
                __instance.onCrushed = __state.OnCrushed;
                __instance.rematOnCrush = __state.RematOnCrush;
                __instance.summonOnCrush = __state.SummonOnCrush;
            }
        }
        finally
        {
            _instance._finishing.Remove(__instance.Pointer);
        }
        if (__exception != null)
            _instance._log.Write("Crush failed: " + __exception);

        return __exception;
    }
}
