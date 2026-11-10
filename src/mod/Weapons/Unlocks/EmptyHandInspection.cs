using HarmonyLib;
using Il2CppSG.Claymore.Interaction;
using Il2CppSG.Claymore.UI.Inspection;
using UnityEngine;

namespace UntilYouFallArchipelago;

internal static class EmptyHandInspection
{
    private static LogChannel? _log;
    private static bool _logged;
    private static string _lastState = "";

    public static void Install(HarmonyLib.Harmony harmony, Action<string> log)
    {
        _log = log.Channel("APInspection");
        harmony.PatchAll(typeof(EmptyHandInspection));
    }

    [HarmonyPatch(typeof(HandUIEmitter), nameof(HandUIEmitter.LateUpdate))]
    [HarmonyPrefix]
    private static bool LateUpdate(HandUIEmitter __instance)
    {
        var hand = __instance.currentHand;
        if (
            !__instance.hasHand
            || hand == null
            || hand.BoundArmament != null
            || __instance.panels == null
            || __instance.isUIPanelActivated == null
        )
            return true;
        if (__instance.panels.Count != __instance.panelKeys.Length)
            return true;

        var crushable = hand.HeldCrushable;
        var panel = crushable == null ? null : __instance.GetPanel();
        var active = __instance.activePanel;
        if (panel != active)
        {
            if (active != null)
            {
                active.Deactivate();
                __instance.isUIPanelActivated.Value = false;
            }
            __instance.activePanel = panel;
            active = panel;
        }
        if (active == null)
            return false;
        var activated = __instance.isUIPanelActivated;
        var state =
            $"{__instance.name} crushable={crushable?.name} panel={panel?.GetInstanceID()} activated={activated.Value}({activated.name}) isActive={active.IsActive} open={__instance.ShouldBeOpen()}";
        if (state != _lastState)
        {
            _lastState = state;
            _log?.Write($"Weaponless hand state {state}");
        }
        if (activated.Value)
        {
            if (__instance.ShouldBeOpen())
                __instance.DoFollow();
            else
            {
                active.Deactivate();
                activated.Value = false;
            }
        }
        else if (__instance.ShouldBeOpen())
        {
            __instance.CatchupPosition();
            var borrowed = hand.OtherHand?.BoundArmament;
            hand._BoundArmament_k__BackingField = borrowed;
            try
            {
                active.Activate(hand, crushable);
            }
            finally
            {
                hand._BoundArmament_k__BackingField = null;
            }
            activated.Value = active.IsActive;
            if (!_logged)
            {
                _logged = true;
                _log?.Write(
                    $"Hand panel opened weithout weapon hand={hand.name} panel={active.name} active={active.IsActive}"
                );
            }
        }

        return false;
    }
}
