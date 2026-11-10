using HarmonyLib;
using Il2CppSG.Claymore.Armaments.Traits;
using Il2CppSG.Claymore.Interaction;

namespace UntilYouFallArchipelago;

// Fix game not recognizing super enhance buffs as actual super levels
internal static class SuperMaxGuard
{
    private static LogChannel _log = null!;
    private static int _logged;

    public static void Install(HarmonyLib.Harmony harmony, Action<string> log)
    {
        _log = log.Channel("APAbilities");
        harmony.PatchAll(typeof(SuperMaxGuard));
    }

    [HarmonyPatch(typeof(UpgradeSuperTrait), "CanBeUsed")]
    [HarmonyPostfix]
    private static void Postfix(PlayerHand __0, ref ArmamentAugment.UsageResult __result)
    {
        try
        {
            var ability = __0?.BoundArmament?.Ability;
            if (ability == null)
                return;

            var capped = PlayerBonusRules.IsSuperMaxed(ability.AbilityLevel, ability.MaxAbilityLevel);
            if (_logged < 20 && (capped || __result != ArmamentAugment.UsageResult.Valid))
            {
                _logged++;
                _log.Write(
                    $"Enhance super eligibility level={ability.AbilityLevel} max={ability.MaxAbilityLevel} vanilla={__result} capped={capped}"
                );
            }
            if (capped && __result == ArmamentAugment.UsageResult.Valid)
                __result = ArmamentAugment.UsageResult.SuperMaxed;
        }
        catch (Exception ex)
        {
            _log.Write("Enhance super guard failed: " + ex.Message);
        }
    }
}
