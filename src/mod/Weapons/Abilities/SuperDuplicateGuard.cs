using HarmonyLib;
using Il2CppSG.Claymore.Armaments;

namespace UntilYouFallArchipelago;

// Fix game crashing with supers that check for both hands when you only have one hand
internal static class SuperDuplicateGuard
{
    private static LogChannel _log = null!;
    private static int _logged;

    public static void Install(HarmonyLib.Harmony harmony, Action<string> log)
    {
        _log = log.Channel("APAbilities");
        harmony.PatchAll(typeof(SuperDuplicateGuard));
    }

    [HarmonyPatch(typeof(ArmamentAbilityUser), "OnSuperActivated")]
    [HarmonyPrefix]
    private static void Prefix(ArmamentAbilityUser __instance, out AbilityTree? __state)
    {
        __state = null;
        try
        {
            var tree = __instance.Armament?.ID?.AbilityTree;
            if (tree == null || !tree.preventDuplicateAbility)
                return;
            var side = (int)__instance.currentHand == 1 ? __instance.rightHandState : __instance.leftHandState;
            var other = side?.GetState()?.Armament;
            if (other?.instance?.Ability != null)
                return;

            tree.preventDuplicateAbility = false;
            __state = tree;
            if (++_logged <= 5)
                _log.Write(
                    $"Duplicate ability check skipped (other hand empty) armament={__instance.Armament?.ID?.GUID}"
                );
        }
        catch (Exception ex)
        {
            _log.Write("Duplicate guard failed: " + ex.Message);
        }
    }

    [HarmonyPatch(typeof(ArmamentAbilityUser), "OnSuperActivated")]
    [HarmonyFinalizer]
    private static void Restore(AbilityTree? __state)
    {
        if (__state != null)
            __state.preventDuplicateAbility = true;
    }
}
