using HarmonyLib;
using Il2CppSG.Claymore.Analytics;
using Il2CppSG.Claymore.Player;

namespace UntilYouFallArchipelago;

// Prevents a bunch of crashes that happen when missing a hand or weapon gets duplicated
internal sealed class SingleWeaponAnalytics
{
    private static SingleWeaponAnalytics _instance = null!;
    private readonly Func<bool> _active;
    private readonly LogChannel _log;
    private readonly HashSet<string> _logged = [];

    public SingleWeaponAnalytics(HarmonyLib.Harmony harmony, Func<bool> active, Action<string> log)
    {
        _instance = this;
        _active = active;
        _log = log.Channel("APWeaponAnalytics");
        harmony.PatchAll(typeof(SingleWeaponAnalytics));
    }

    [HarmonyPatch(typeof(PlayerArmamentAnalyticsEvent), nameof(PlayerArmamentAnalyticsEvent.Record))]
    [HarmonyPrefix]
    private static bool RecordPlayerArmament(PlayerArmamentAnalyticsEvent __instance)
    {
        if (!_instance._active())
            return true;

        return _instance.CheckPair(__instance._leftHandState, __instance._rightHandState, "PlayerArmament");
    }

    [HarmonyPatch(typeof(ArmamentAnalyticsEvent), nameof(ArmamentAnalyticsEvent.Record))]
    [HarmonyPrefix]
    private static bool RecordRunArmaments(ArmamentAnalyticsEvent __instance)
    {
        if (!_instance._active())
            return true;

        return _instance.CheckPair(__instance._leftHandState, __instance._rightHandState, "RunArmaments");
    }

    [HarmonyPatch(typeof(PlayerKilledArmamentAnalyticsEvent), nameof(PlayerKilledArmamentAnalyticsEvent.Record))]
    [HarmonyPrefix]
    private static bool RecordPlayerKilled(
        PlayerKilledArmamentAnalyticsEvent __instance,
        PlayerKilledArmamentAnalyticsEvent.HandID __0
    )
    {
        if (!_instance._active())
            return true;

        var hand =
            __0 == PlayerKilledArmamentAnalyticsEvent.HandID.Left
                ? __instance._leftHandState
                : __instance._rightHandState;

        return _instance.Check(WeaponAnalyticsRules.AllowHand(hand?.Armament != null), "PlayerKilled");
    }

    [HarmonyPatch(typeof(BeginRunAnalyticsEvent), nameof(BeginRunAnalyticsEvent.Record))]
    [HarmonyPrefix]
    private static bool RecordBeginRun(BeginRunAnalyticsEvent __instance)
    {
        if (!_instance._active())
            return true;

        return _instance.CheckPair(__instance._leftHandState, __instance._rightHandState, "BeginRun");
    }

    private static bool SameArmament(PlayerHandState? left, PlayerHandState? right)
    {
        var leftArmament = left?.Armament;
        var rightArmament = right?.Armament;
        return leftArmament != null && rightArmament != null && leftArmament.Pointer == rightArmament.Pointer;
    }

    private bool CheckPair(PlayerHandState? left, PlayerHandState? right, string eventName) =>
        Check(
            WeaponAnalyticsRules.AllowPair(left?.Armament != null, right?.Armament != null, SameArmament(left, right)),
            eventName
        );

    private bool Check(bool allow, string eventName)
    {
        if (!allow && _logged.Add(eventName))
            _log.Write($"Skipped {eventName}: empty or duplicate hand");

        return allow;
    }
}
