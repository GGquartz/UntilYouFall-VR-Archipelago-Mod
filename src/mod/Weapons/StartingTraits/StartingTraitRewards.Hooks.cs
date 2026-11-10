using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSG.Claymore.Armaments;
using Il2CppSG.Claymore.Interaction;
using Il2CppSG.Claymore.Interaction.Rewards;
using Il2CppSG.Claymore.Level;
using Il2CppSG.GlobalEvents;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal sealed partial class StartingTraitRewards : IDisposable
{
    private static bool EnableSpawner(RewardSpawner __instance) => !_instance._groups.ContainsKey(__instance.Pointer);

    private static bool AllowSpawn(RewardSpawner __instance, int __0)
    {
        if (!_instance._groups.TryGetValue(__instance.Pointer, out var group))
            return true;

        return !group.Retired
            && _instance._run != null
            && __0 >= 0
            && __0 < group.Items.Length
            && StartingTraitRules.Available(_instance._run, group.Items[__0]);
    }

    private static bool Select(RewardSpawner __instance)
    {
        if (!_instance._groups.TryGetValue(__instance.Pointer, out var group))
            return true;

        __instance.selectedSpawnRewards = new Il2CppReferenceArray<ArmamentAugment>(group.Rewards);
        return false;
    }

    private static void Register(RewardInteractable __instance, ArmamentAugment __0)
    {
        var spawner = __instance.GetComponentInParent<RewardSpawner>();
        if (spawner == null || !_instance._groups.TryGetValue(spawner.Pointer, out var group) || group.Retired)
            return;
        var trait = StartingTraitRules.All.FirstOrDefault(item => item.RewardGuid == __0.GUID);
        if (trait == null)
            return;

        _instance._rewards[__instance.Pointer] = (__instance, trait.Id, group);
        __instance.onCrushed = new UnityEvent();
        __instance.onRewardDropped = _instance._localEvent;
        __instance.onRewardGrabbed = _instance._localEvent;
        __instance.onRewardCrushStart = _instance._localEvent;
        __instance.onRewardCrushCancel = _instance._localEvent;
        __instance.rematOnCrush = false;
        __instance.summonOnCrush = true;

        _instance._log.Write($"native crystal ready item={trait.Id} instance={__instance.GetInstanceID()}");
    }

    private string FormatEligibilityForLog(PlayerHand? hand)
    {
        if (hand == null)
            return "hand=null";
        var weapon = hand.BoundArmament;
        if (weapon == null)
            return $"hand={hand.GetInstanceID()}; scene={hand.gameObject.scene.name}; weapon=null";

        var results = _assets
            .GroupBy(entry => entry.Value.CanBeUsed(hand))
            .Select(group => $"{group.Key}:{group.Count()}");

        return $"hand={hand.GetInstanceID()} scene={hand.gameObject.scene.name} weapon={weapon.name} "
            + $"ready={weapon.IsReady} sockets={weapon.AvailableSockets} rewardTraits={weapon.TraitsAppliedByRewards} "
            + $"nativeResults={string.Join(",", results)}";
    }

    private static bool Usable(ArmamentAugment reward, PlayerHand? hand) =>
        hand?.BoundArmament != null
        && hand.BoundArmament.AvailableSockets > 0
        && reward.CanBeUsed(hand) == ArmamentAugment.UsageResult.Valid;

    private static bool KeepOffer(RewardInteractable __instance) =>
        !_instance._rewards.TryGetValue(__instance.Pointer, out var entry) || entry.Group.Retired;

    private static void CanCrush(RewardInteractable __instance, ref bool __result)
    {
        if (!_instance._rewards.TryGetValue(__instance.Pointer, out var entry))
            return;

        __result =
            __result
            && _instance._identity == _instance._client.StartingTraitIdentity
            && _instance._run is { } run
            && StartingTraitRules.Available(run, entry.Item)
            && Usable(__instance.Upgrade, __instance.ActiveHand);
    }

    private static bool BeforeFinish(CrushInteractable __instance)
    {
        if (!_instance._rewards.ContainsKey(__instance.Pointer))
            return true;
        var reward = __instance.TryCast<RewardInteractable>();
        if (reward == null || !reward.CanCrush)
            return false;

        reward.onCrushed = new UnityEvent();
        return true;
    }

    internal static bool Owns(RewardInteractable reward) =>
        _instance != null && _instance._rewards.ContainsKey(reward.Pointer);

    private static bool Complete(RewardInteractable __instance)
    {
        if (!_instance._rewards.TryGetValue(__instance.Pointer, out var entry))
            return true;

        try
        {
            var hand = __instance.ActiveHand;
            var reward = __instance.Upgrade;
            var run = _instance._run;
            if (
                run == null
                || _instance._identity != _instance._client.StartingTraitIdentity
                || !Usable(reward, hand)
                || !_instance._client.TakeStartingTrait(run.Key, entry.Item)
            )
                return false;

            _instance._run = StartingTraitRules.Pick(run, entry.Item);
            var index = Array.IndexOf(entry.Group.Items, entry.Item);
            if (index >= 0 && entry.Group.Spawner != null)
                entry.Group.Spawner.rewardTendrilController?.SetTendrilEnabled(index, false);
            __instance.EndShake();
            hand!.State.ApplyUpgrade(reward);
            var weapon = hand!.BoundArmament;
            weapon.AddTraitUpgrade(reward.Trait);
            foreach (var mod in reward.OneTimeBuffs)
                ArmamentStats.ApplyMod(weapon.Stats.GetVariable(mod.Variable), mod, 1f);
            reward.OnCrushComplete(hand);
            _instance._manager!.saveManager.SaveRun();
            _instance._log.Write(
                $"applied item={entry.Item} picks={_instance._run.Taken.Length}/{run.PickLimit} sockets={weapon.AvailableSockets}"
            );
            _instance.PlayFeedback(true, __instance.transform);
        }
        catch (Exception exception)
        {
            _instance._log.Write($"crush failed {exception}");
        }

        return false;
    }
}
