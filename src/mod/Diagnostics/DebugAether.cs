using HarmonyLib;
using Il2CppSG.Claymore.UI;

namespace UntilYouFallArchipelago;

internal static class DebugAether
{
    private static LogChannel _log = null!;
    private static bool _enabled;

    public static void Install(HarmonyLib.Harmony harmony, bool enabled, Action<string> log)
    {
        _log = log.Channel("DebugAether");
        _enabled = enabled;
        if (!enabled)
        {
            log.Write("DebugAether", "No aether added");
            return;
        }

        harmony.Patch(
            AccessTools.Method(typeof(ProfileUI), nameof(ProfileUI.LoadProfile)),
            prefix: new HarmonyMethod(typeof(DebugAether), nameof(IsNewProfile)),
            postfix: new HarmonyMethod(typeof(DebugAether), nameof(Grant))
        );

        log.Write("DebugAether", $"Enabled - New save will get {DebugAetherRules.GrantAmount:N0} aether");
    }

    private static void IsNewProfile(ProfileUI __instance, out bool __state)
    {
        __state = DebugAetherRules.ShouldGrant(_enabled, __instance._isNewGame);
    }

    private static void Grant(ProfileUI __instance, bool __state)
    {
        if (!__state || __instance._currencyVariable == null || __instance._saveManager == null)
            return;

        var before = __instance._currencyVariable.Value;
        __instance._currencyVariable.Value = DebugAetherRules.Apply(before);
        __instance._saveManager.SaveEconomy();
        _log.Write($"New profile {__instance._profileIndex} Aether={before}->{__instance._currencyVariable.Value}");
    }
}
