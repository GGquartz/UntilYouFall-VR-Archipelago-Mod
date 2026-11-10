using HarmonyLib;
using Il2Cpp;
using Il2CppSG.Claymore.Interaction;
using Il2CppSG.GlobalEvents.Variables;
using UnityEngine;

namespace UntilYouFallArchipelago;

internal sealed class AetherMultiplier
{
    private static AetherMultiplier _instance = null!;
    private readonly Func<int> _itemCount;
    private readonly LogChannel _log;
    private (int, int, int) _lastScale;
    private (int, float, int) _lastPayout;
    private int _loggedLevel = -1;

    public AetherMultiplier(HarmonyLib.Harmony harmony, Func<int> itemCount, Action<string> log)
    {
        _instance = this;
        _itemCount = itemCount;
        _log = log.Channel("APMultiplier");
        harmony.Patch(
            AccessTools.PropertyGetter(
                typeof(AetherMoteArmamentAugment),
                nameof(AetherMoteArmamentAugment.AetherAmount)
            ),
            prefix: new HarmonyMethod(typeof(AetherMultiplier), nameof(EnterAugment)),
            postfix: new HarmonyMethod(typeof(AetherMultiplier), nameof(ScaleAmount))
        );
        harmony.Patch(
            AccessTools.PropertyGetter(
                typeof(RoomCompletionHistory.RoomData),
                nameof(RoomCompletionHistory.RoomData.AetherRewardAmount)
            ),
            postfix: new HarmonyMethod(typeof(AetherMultiplier), nameof(ScaleHistoryAmount))
        );
        harmony.Patch(
            AccessTools.Method(typeof(RoomCompletionHistory), nameof(RoomCompletionHistory.CalculatePayout)),
            prefix: new HarmonyMethod(typeof(AetherMultiplier), nameof(OverrideEnemyPayout)),
            postfix: new HarmonyMethod(typeof(AetherMultiplier), nameof(LogPayout))
        );
    }

    public int ItemCount() => AetherMultiplierRules.Clamp(_itemCount());

    private int DifficultyLevel()
    {
        var level = GameDifficulty.Setting() ?? 0;
        if (level != _loggedLevel)
        {
            _loggedLevel = level;
            _log.Write($"Difficulty level={level} total={AetherMultiplierRules.DifficultyTotal(level):F2}");
        }

        return level;
    }

    private static bool _inAugment;

    private static void EnterAugment() => _inAugment = true;

    private static void OverrideEnemyPayout(ref float multiplier)
    {
        if (_inAugment)
            return;

        var level = _instance.DifficultyLevel();
        var count = _instance.ItemCount();
        var native = multiplier;
        multiplier = AetherMultiplierRules.Payout(native, count, level);

        _instance._log.Write(
            $"Nonreward payout native={native:F2} level={level} apCount={count} multiplier={multiplier:F2}"
        );
    }

    private static void ScaleAmount(AetherMoteArmamentAugment __instance, ref int __result)
    {
        _inAugment = false;
        var count = _instance.ItemCount();
        var level = _instance.DifficultyLevel();
        var payout = __instance.payoutMultiplier != null ? __instance.payoutMultiplier.Value : 1f;
        var scaled = AetherMultiplierRules.Scale(__result, payout, count, level);
        if (_instance._lastScale != (__result, count, level))
        {
            _instance._lastScale = (__result, count, level);
            _instance._log.Write(
                $"Scale reward={__instance.name} vanilla={__result} payout={payout:F2} apCount={count} level={level} scaled={scaled}"
            );
        }
        __result = scaled;
    }

    private static void LogPayout(int basePayout, float multiplier, int __result)
    {
        if (_instance._lastPayout == (basePayout, multiplier, __result))
            return;

        _instance._lastPayout = (basePayout, multiplier, __result);
        _instance._log.Write($"Calculate payout base={basePayout} multiplier={multiplier:F2} result={__result}");
    }

    private static void ScaleHistoryAmount(RoomCompletionHistory.RoomData __instance, ref int __result)
    {
        if (!__instance.IsCrushedAetherReward)
            return;
        var augment = __instance.AetherMoteArmamentAugment;
        if (augment == null)
            return;

        var liveAmount = augment.AetherAmount;
        if (__result != liveAmount)
            _instance._log.Write(
                $"Cashout reward corrected history={__result} live={liveAmount} reward={augment.name}"
            );
        __result = liveAmount;
    }
}
