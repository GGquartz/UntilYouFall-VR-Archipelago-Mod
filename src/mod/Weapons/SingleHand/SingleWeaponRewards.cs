using HarmonyLib;
using Il2CppSG.Claymore.Armaments;
using Il2CppSG.Claymore.Armaments.Traits;
using Il2CppSG.Claymore.Interaction;
using Il2CppSG.Claymore.Interaction.Rewards;
using Il2CppSG.GlobalEvents.Variables;

namespace UntilYouFallArchipelago;

internal sealed class SingleWeaponRewards
{
    private static SingleWeaponRewards _instance = null!;
    private readonly Func<bool> _active;
    private readonly LogChannel _log;
    private readonly LogChannel _rewardDiagLog;
    private readonly HashSet<string> _healthDiagnostics = [];
    private bool _loggedEmpty;

    public SingleWeaponRewards(HarmonyLib.Harmony harmony, Func<bool> active, Action<string> log)
    {
        _instance = this;
        _active = active;
        _log = log.Channel("APRewards");
        _rewardDiagLog = log.Channel("APRewardDiag");
        foreach (
            var type in new[]
            {
                typeof(EmbeddedTraitAugment),
                typeof(TraitAugment),
                typeof(BaseArmamentTrait),
                typeof(StatModArmamentTrait),
                typeof(UpgradeSuperTrait),
            }
        )
            Patch(harmony, type, "CanBeUsed", nameof(CanUseTrait));
        Patch(harmony, typeof(RestoreHealthAugment), "CanBeUsed", nameof(UseHealthHand));
        harmony.Patch(
            AccessTools.Method(typeof(RestoreHealthAugment), "CanBeUsed"),
            postfix: new HarmonyMethod(typeof(SingleWeaponRewards), nameof(LogHealthResult))
        );
        harmony.Patch(
            AccessTools.Method(typeof(AugmentUpgradeWeightedRandomSet), "RemoveInvalidElementsRecursive"),
            prefix: new HarmonyMethod(typeof(SingleWeaponRewards), nameof(BeforeHealthFilter)),
            postfix: new HarmonyMethod(typeof(SingleWeaponRewards), nameof(AfterHealthFilter))
        );
        Patch(harmony, typeof(EmbeddedTraitAugment), "Apply", nameof(UseWeapon));
        Patch(harmony, typeof(EmbeddedTraitAugment), "Remove", nameof(UseWeapon));
        Patch(harmony, typeof(ChargeSuperAugment), "OnCrush", nameof(UseWeapon));
        Patch(harmony, typeof(RewardInteractable), "get_CanCrush", nameof(CanCrush));
        Patch(harmony, typeof(RewardInteractable), "OnCrushComplete", nameof(Complete));
        harmony.Patch(
            AccessTools.Method(typeof(RewardInteractable), "OnCrushComplete"),
            postfix: new HarmonyMethod(typeof(SingleWeaponRewards), nameof(LogCompleted))
        );
    }

    private static void Patch(HarmonyLib.Harmony harmony, Type type, string method, string prefix) =>
        harmony.Patch(AccessTools.Method(type, method), prefix: new HarmonyMethod(typeof(SingleWeaponRewards), prefix));

    private static bool UseWeapon(PlayerHand __0)
    {
        var allow = SingleWeaponRewardRules.AllowWeapon(_instance._active(), __0?.BoundArmament != null);
        if (!allow && !_instance._loggedEmpty)
        {
            _instance._loggedEmpty = true;
            _instance._log.Write("Rejected weapon reward for an empty hand");
        }

        return allow;
    }

    private static bool CanUseTrait(PlayerHand __0, ref ArmamentAugment.UsageResult __result)
    {
        if (UseWeapon(__0))
            return true;

        __result = ArmamentAugment.UsageResult.NoSockets;
        return false;
    }

    private static void UseHealthHand(ref PlayerHand __0)
    {
        LogHealthEligibility("eligibility-enter", null, __0, "pending");
        if (!_instance._active() || __0 == null || __0.BoundArmament != null)
            return;
        var other = __0.OtherHand;
        if (other?.BoundArmament == null)
            return;

        __0 = other;
        _instance._log.Write("Vitality eligibility redirected");
    }

    private static void BeforeHealthFilter(AugmentUpgradeWeightedRandomSet __0, ref PlayerHand __1, ref PlayerHand __2)
    {
        if (_instance._active())
        {
            if (__1 == null && __2 != null && __2.BoundArmament != null)
            {
                __1 = __2;
                _instance._log.Write("Vitality filter replaced to left");
            }
            else if (__2 == null && __1 != null && __1.BoundArmament != null)
            {
                __2 = __1;
                _instance._log.Write("Vitality filter replaced to right");
            }
        }

        LogHealthEligibility($"filter-before-left:{__0?.Pointer}", null, __1, "pending");
        LogHealthEligibility($"filter-before-right:{__0?.Pointer}", null, __2, "pending");
    }

    private static void AfterHealthFilter(AugmentUpgradeWeightedRandomSet __0, PlayerHand __1, PlayerHand __2)
    {
        LogHealthEligibility($"filter-after-left:{__0?.Pointer}", null, __1, "returned");
        LogHealthEligibility($"filter-after-right:{__0?.Pointer}", null, __2, "returned");
    }

    private static void LogHealthResult(
        RestoreHealthAugment __instance,
        PlayerHand __0,
        ref ArmamentAugment.UsageResult __result
    )
    {
        var current = __result;
        StageRewardDiagnosticGuard.Run(
            () =>
            {
                var player = __0?.PlayerCombatant?.PlayerHealth;
                if (
                    _instance._active()
                    && __instance.failOnFullHealth
                    && __0 != null
                    && __0.BoundArmament == null
                    && current == ArmamentAugment.UsageResult.Valid
                    && player != null
                    && player.CurrentHealth >= player.MaxHealth
                )
                {
                    current = ArmamentAugment.UsageResult.HealthFull;
                    _instance._log.Write("Vitality eligibility forced HealthFull for empty hand at full HP");
                }
            },
            exception => _instance._log.Write($"Vitality override failed: {exception.Message}")
        );

        __result = current;
        LogHealthEligibility("eligibility-result", __instance, __0, __result.ToString());
    }

    private static void LogHealthEligibility(
        string phase,
        RestoreHealthAugment? reward,
        PlayerHand? hand,
        string result
    ) =>
        StageRewardDiagnosticGuard.Run(
            () =>
            {
                var player = hand?.PlayerCombatant?.PlayerHealth;
                var message =
                    $"Vitality {phase} active={_instance._active()} reward={reward?.guid} "
                    + $"failOnFullHealth={reward?.failOnFullHealth} hand={hand?.Pointer} bound={hand?.BoundArmament?.Pointer} "
                    + $"handAlive={hand != null} playerAlive={player != null} "
                    + $"otherHand={hand?.OtherHand?.Pointer} otherBound={hand?.OtherHand?.BoundArmament?.Pointer} "
                    + $"HP={player?.CurrentHealth:R}/{player?.MaxHealth:R} result={result}";
                if (_instance._healthDiagnostics.Add(message))
                    _instance._log.Write(message);
            },
            exception => _instance._log.Write($"Vitality log failed: {exception.Message}")
        );

    private static bool RequiresWeapon(ArmamentAugment reward) =>
        reward.TryCast<EmbeddedTraitAugment>() != null
        || reward.TryCast<TraitAugment>() != null
        || reward.TryCast<ChargeSuperAugment>() != null;

    private static bool CanCrush(RewardInteractable __instance, ref bool __result)
    {
        if (!_instance._active() || !RequiresWeapon(__instance.Upgrade) || UseWeapon(__instance.ActiveHand))
            return true;

        __result = false;
        return false;
    }

    private static bool Complete(RewardInteractable __instance)
    {
        if (StartingTraitRewards.Owns(__instance))
            return false;
        LogReward("before", __instance);
        if (!_instance._active())
            return true;
        var hand = __instance.ActiveHand;
        var reward = __instance.Upgrade;
        if (RequiresWeapon(reward) && !UseWeapon(hand))
            return false;
        var weapon = hand?.BoundArmament;
        var trait = reward.Trait;
        if (
            !SingleWeaponRewardRules.CompleteSafely(
                true,
                weapon != null,
                hand?.OtherHand?.BoundArmament != null,
                trait != null
            )
        )
            return true;

        __instance.EndShake();
        __instance.completionhistory.SetCrushedReward(reward);
        reward.OnCrush(hand!);
        weapon = hand!.BoundArmament;
        trait = reward.Trait;
        if (weapon != null)
        {
            if (trait != null)
            {
                weapon.AddTraitUpgrade(trait);
                var count = weapon.TraitsAppliedByRewards + weapon.TraitsAppliedByLevel;
                var other = hand.OtherHand?.BoundArmament;
                var total = count + (other == null ? 0 : other.TraitsAppliedByRewards + other.TraitsAppliedByLevel);
                if (count >= __instance.singleWeaponTraitTarget.Value)
                    __instance.traitsOnSingleWeaponAchievement.IncrementAchievementProgress(1);
                if (total >= __instance.bothWeaponsTraitTarget.Value)
                    __instance.traitsOnBothWeaponsAchievement.IncrementAchievementProgress(1);
                _instance._log.Write($"Applied {reward.DeveloperName} traits={count} total={total}");
            }
            foreach (var mod in reward.OneTimeBuffs)
                ArmamentStats.ApplyMod(weapon.Stats.GetVariable(mod.Variable), mod, 1f);
        }

        reward.OnCrushComplete(hand!);
        return false;
    }

    private static void LogCompleted(RewardInteractable __instance) => LogReward("after", __instance);

    private static void LogReward(string phase, RewardInteractable interactable)
    {
        try
        {
            var reward = interactable.Upgrade;
            var weapon = interactable.ActiveHand?.BoundArmament;
            if (reward == null)
                return;
            var mods = reward.OneTimeBuffs.Select(mod =>
            {
                var resolved = weapon?.Stats?.GetVariable(mod.Variable);
                return $"{mod.Variable?.name ?? "?"}->{resolved?.name ?? "null"} amount={mod.Amount} value={Value(resolved)}";
            });

            _instance._rewardDiagLog.Write(
                $"{phase}; reward={reward.DeveloperName} weapon={weapon?.name ?? "null"} buffs=[{string.Join(" | ", mods)}]"
            );
        }
        catch (Exception exception)
        {
            _instance._rewardDiagLog.Write($"{phase} failed: {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static string Value(Variable? variable)
    {
        if (variable == null)
            return "null";
        var integer = variable.TryCast<IntVariable>();
        if (integer != null)
            return integer.Value.ToString();

        var number = variable.TryCast<FloatVariable>();
        return number == null ? variable.GetType().Name : number.Value.ToString("F3");
    }
}
