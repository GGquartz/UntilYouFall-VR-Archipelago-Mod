using HarmonyLib;
using Il2CppSG.Claymore.Entities;
using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.Movement.Dash;
using UnityEngine;

namespace UntilYouFallArchipelago;

internal sealed class ExhaustionTrap : TimedTrap
{
    private static ExhaustionTrap _instance = null!;
    private IntPtr _loggedOwner;
    private readonly ExhaustionDashRecharge _recharge = new();

    public ExhaustionTrap(
        HarmonyLib.Harmony harmony,
        Func<PlayerHealth?> player,
        Func<bool> ready,
        Func<LevelManager?> level,
        Func<bool> dodgeActive,
        Func<int> durationSeconds,
        Action<string> log
    )
        : base(
            new TimedTrapState(),
            "Exhaustion",
            "APExhaustionTrap",
            () => TimedTrapHudLayout.ExhaustionRow(dodgeActive()),
            player,
            ready,
            level,
            durationSeconds,
            log
        )
    {
        _instance = this;
        harmony.PatchAll(typeof(ExhaustionTrap));
    }

    protected override void OnActivated()
    {
        var dash = Resources.FindObjectsOfTypeAll<PlayerDash>().FirstOrDefault(candidate => Matches(candidate));
        if (dash == null)
            throw new InvalidOperationException("No current scene PlayerDash; receipt voided.");

        ApplyRecharge(dash);
        _loggedOwner = IntPtr.Zero;
        LogOwner(dash);
    }

    protected override void OnEnded() => _recharge.Clear();

    private void ApplyRecharge(PlayerDash dash)
    {
        if (_recharge.Apply(dash))
            _log.Write(
                $"Recharge interval x3 applied interval={dash._dashChargeRestoreTimeVariable.Value:F4}s charges={dash._dashChargesVariable.Value}."
            );
    }

    private bool Matches(PlayerDash dash) =>
        dash != null
        && dash.isActiveAndEnabled
        && dash.gameObject.scene.IsValid()
        && dash._levelManager?.Pointer == _level()?.Pointer
        && dash.PlayerEntity != null
        && dash.PlayerEntity.Pointer == _player()?.Entity?.Pointer;

    private bool Applies(PlayerDash dash)
    {
        var manager = _level();
        return Active && _state.InRun && _ready() && manager.InCombatRoom() && Matches(dash);
    }

    private void LogOwner(PlayerDash dash)
    {
        if (_loggedOwner == dash.Pointer)
            return;
        _loggedOwner = dash.Pointer;
        _log.Write(
            $"Native owner bound dash={dash.Pointer} level={dash._levelManager?.Pointer} charges={dash._dashChargesVariable.Value} max={dash._dashChargesVariable.Max.Value} remaining={_state.RemainingSeconds:F2} scene={dash.gameObject.scene.name}"
        );
    }

    [HarmonyPatch(typeof(PlayerDash), nameof(PlayerDash.Update))]
    [HarmonyPrefix]
    private static void BeginUpdate(PlayerDash __instance, out bool __state)
    {
        __state = false;
        try
        {
            if (!_instance.Matches(__instance))
                return;
            if (!_instance.Applies(__instance))
            {
                _instance._recharge.Clear();
                return;
            }

            __state = true;
            _instance.ApplyRecharge(__instance);
            _instance.LogOwner(__instance);
        }
        catch (Exception exception)
        {
            _instance.EndStage("update-prefix-error");
            _instance._log.Write($"Recharge penalty failed {exception}");
        }
    }

    [HarmonyPatch(typeof(PlayerDash), nameof(PlayerDash.Update))]
    [HarmonyFinalizer]
    private static Exception? EndUpdate(PlayerDash __instance, bool __state, Exception? __exception)
    {
        if (!__state)
            return __exception;

        try
        {
            if (__exception != null)
                _instance.EndStage("native-update-error");
        }
        catch (Exception exception)
        {
            _instance.EndStage("update-finalizer-error");
            _instance._log.Write($"Recharge cleanup failed {exception}");
        }

        return __exception;
    }

    [HarmonyPatch(typeof(LevelManager), nameof(LevelManager.LoadEndRun), typeof(bool))]
    [HarmonyPrefix]
    private static void OnEndRun(LevelManager __instance)
    {
        if (_instance._level()?.Pointer == __instance.Pointer)
            _instance.EndRun("end-run");
    }
}
