using HarmonyLib;
using Il2CppSG.Claymore.SaveSystem.SavedElements;
using Il2CppSG.Claymore.UI;

namespace UntilYouFallArchipelago;

internal sealed class ProfileWeaponIcons
{
    private static SavedWeaponPreview? _preview;

    public ProfileWeaponIcons(HarmonyLib.Harmony harmony, Action<string> log)
    {
        harmony.Patch(
            AccessTools.Method(typeof(ProfileUI), nameof(ProfileUI.AttemptLoad)),
            prefix: new HarmonyMethod(typeof(ProfileWeaponIcons), nameof(BeginPreview)),
            postfix: new HarmonyMethod(typeof(ProfileWeaponIcons), nameof(ShowSavedIcons)),
            finalizer: new HarmonyMethod(typeof(ProfileWeaponIcons), nameof(EndPreview))
        );
        harmony.Patch(
            AccessTools.Method(typeof(SavedArmamentEquip), nameof(SavedArmamentEquip.Deserialize)),
            postfix: new HarmonyMethod(typeof(ProfileWeaponIcons), nameof(ReadSavedHand))
        );
    }

    private static void BeginPreview(out SavedWeaponPreview? __state)
    {
        __state = _preview;
        _preview = new SavedWeaponPreview();
    }

    private static void ReadSavedHand(SavedArmamentEquip __instance, string? __0)
    {
        if (_preview != null && __instance.hand != null)
            _preview.Read(__instance.hand.GetState().Pointer, __0);
    }

    private static void ShowSavedIcons(ProfileUI __instance)
    {
        if (_preview == null)
            return;

        var left = __instance._leftHandState;
        var right = __instance._rightHandState;
        if (
            left != null
            && __instance._leftHandArmament != null
            && _preview.ShowIcon(left.GetState().Pointer) is bool showLeft
        )
            __instance._leftHandArmament.enabled = showLeft;
        if (
            right != null
            && __instance._rightHandArmament != null
            && _preview.ShowIcon(right.GetState().Pointer) is bool showRight
        )
            __instance._rightHandArmament.enabled = showRight;
    }

    private static Exception? EndPreview(Exception? __exception, SavedWeaponPreview? __state)
    {
        _preview = __state;
        return __exception;
    }
}
