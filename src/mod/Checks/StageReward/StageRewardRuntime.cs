using HarmonyLib;
using Il2CppSG.Claymore.Interaction;
using Il2CppSG.Claymore.UI.Inspection;
using Il2CppSG.ProtoFramework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal sealed class StageRewardRuntime : IDisposable
{
    private sealed class ActiveOffer
    {
        public ActiveOffer(
            StageRewardOfferDecision decision,
            ArmamentAugment historyPlaceholder,
            string title,
            string description,
            string color
        )
        {
            Decision = decision;
            HistoryPlaceholder = historyPlaceholder;
            Title = title;
            Description = description;
            Color = color;
        }

        public StageRewardOfferDecision Decision { get; }
        public ArmamentAugment HistoryPlaceholder { get; }
        public string Title { get; }
        public string Description { get; }
        public string Color { get; }
        public RewardInteractable? Reward { get; set; }
        public IItemVisual? Visual { get; set; }
        public long VisualRetryAt { get; set; }
        public bool HintSent { get; set; }
        public bool Finished { get; set; }
    }

    private static StageRewardRuntime _instance = null!;

    [ThreadStatic]
    private static ActiveOffer? _completingOffer;
    public static bool IsCompletingOffer => _completingOffer != null;
    private readonly ArchipelagoClient _client;
    private readonly Func<string> _room;
    private readonly LogChannel _log;
    private readonly bool _forceEveryAether;
    private readonly Dictionary<int, ActiveOffer> _augmentOffers = [];
    private readonly Dictionary<int, ActiveOffer> _rewardOffers = [];

    public StageRewardRuntime(
        HarmonyLib.Harmony harmony,
        ArchipelagoClient client,
        Func<string> room,
        Action<string> log,
        bool forceEveryAether
    )
    {
        _instance = this;
        _client = client;
        _room = room;
        _log = log.Channel("APStageReward");
        _forceEveryAether = forceEveryAether;
        harmony.PatchAll(typeof(StageRewardRuntime));
    }

    public void Update()
    {
        if (_augmentOffers.Count > 0)
            Safe(UpdateVisuals);
    }

    private void UpdateVisuals()
    {
        foreach (var offer in _augmentOffers.Values)
        {
            if (
                offer.Finished
                || offer.Reward == null
                || offer.Visual != null
                || Environment.TickCount64 < offer.VisualRetryAt
            )
                continue;

            AttachVisual(offer);
        }
    }

    [HarmonyPatch(typeof(RewardSpawner), "PopulateSpawnRewards")]
    [HarmonyPostfix]
    private static void ReplaceSelectedReward(RewardSpawner __instance) =>
        _instance.Safe(() => _instance.TryReplace(__instance));

    private void TryReplace(RewardSpawner spawner)
    {
        ClearRuntime();
        var selected = spawner.selectedSpawnRewards;
        if (selected == null || selected.Count == 0)
            return;

        var aetherSlots = Enumerable
            .Range(0, selected.Count)
            .Select(index =>
            {
                var candidate = selected[index];
                return candidate?.TryCast<AetherMoteArmamentAugment>() != null
                    && StageRewardRuntimeRules.ResourceIdentityComplete(
                        candidate.name,
                        candidate.DeveloperName,
                        candidate.GUID
                    );
            })
            .ToArray();
        if (!aetherSlots.Any(value => value))
            return;

        var eligibleSlots = StageRewardRuntimeRules.UniqueCandidateSlots(
            aetherSlots,
            Enumerable.Range(0, selected.Count).Select(index => Id(selected[index])).ToArray()
        );
        if (!eligibleSlots.Any(value => value))
        {
            _log.Write("Offer skipped, no aether slot");
            return;
        }

        var placeholderIndex = StageRewardRuntimeRules.SelectHistoryPlaceholder(aetherSlots, -1);
        var placeholder = placeholderIndex == null ? FindFallbackPlaceholder() : selected[placeholderIndex.Value];
        if (
            placeholder == null
            || !StageRewardRuntimeRules.ResourceIdentityComplete(
                placeholder.name,
                placeholder.DeveloperName,
                placeholder.GUID
            )
        )
        {
            _log.Write("Offer skipped, no complete vanilla nonaether history placeholder is loaded");
            return;
        }

        var decision = _client.PrepareStageRewardOffer(
            _room(),
            eligibleSlots,
            UnityEngine.Random.Range(0, 100),
            UnityEngine.Random.Range(0, 1_000_000),
            _forceEveryAether
        );
        if (decision == null)
            return;
        if (!_client.TryGetStageRewardInfo(decision.LocationId, out var info))
            return;

        if (decision.AetherSlot < 0 || decision.AetherSlot >= selected.Count)
        {
            _log.Write($"Offer skipped, slot {decision.AetherSlot} outside {selected.Count} rewards");
            return;
        }
        var original = selected[decision.AetherSlot];
        var originalAether = original?.TryCast<AetherMoteArmamentAugment>();
        if (original == null || originalAether == null)
            return;

        var title = StageRewardInspection.Title(info);
        var description = StageRewardInspection.Description(info);
        var offer = new ActiveOffer(decision, placeholder, title, description, StageRewardInspection.Color(info));
        _augmentOffers[Id(original)] = offer;
        var chance = StageRewardProgress.ChancePercent(
            decision.EligibleAttempt,
            (int)(decision.LocationId - StageRewardChecks.BaseId),
            _client.StageRewardCheckCount
        );

        _log.Write(
            $"Changed vanilla reward location={decision.LocationId} attempt={decision.EligibleAttempt} "
                + $"chance={chance}% slot={decision.AetherSlot} "
                + $"forced={_forceEveryAether} replacedAether={originalAether.AetherAmount} "
                + $"resourceName='{original.name}' developer='{original.DeveloperName}' guid='{original.GUID}' "
                + $"altResource='{original.AltResourceKey?.Name ?? "null"}' "
                + $"placeholder='{placeholder.DeveloperName}'"
        );
    }

    private static ArmamentAugment? _fallbackPlaceholder;

    private static ArmamentAugment? FindFallbackPlaceholder() =>
        _fallbackPlaceholder != null
            ? _fallbackPlaceholder
            : _fallbackPlaceholder = Resources
                .FindObjectsOfTypeAll<ArmamentAugment>()
                .FirstOrDefault(candidate =>
                    candidate != null
                    && candidate.name == "Reward_Heal"
                    && candidate.TryCast<AetherMoteArmamentAugment>() == null
                    && StageRewardRuntimeRules.ResourceIdentityComplete(
                        candidate.name,
                        candidate.DeveloperName,
                        candidate.GUID
                    )
                );

    private readonly HashSet<int> _nativeAetherRewards = [];

    [HarmonyPatch(typeof(RewardInteractable), nameof(RewardInteractable.Initialize), typeof(ArmamentAugment))]
    [HarmonyPostfix]
    private static void RegisterReward(RewardInteractable __instance, ArmamentAugment augment) =>
        _instance.Safe(() =>
        {
            if (augment.TryCast<AetherMoteArmamentAugment>() != null)
                _instance._nativeAetherRewards.Add(Id(__instance));
            if (!_instance._augmentOffers.TryGetValue(Id(augment), out var offer))
                return;

            offer.Reward = __instance;
            _instance._rewardOffers[Id(__instance)] = offer;
            ApRewardInspectionPanel.UseLargePanel(__instance, _instance._log.Sink, "APStageReward");
            __instance.maxCrushSeconds = RuneforgeShopRules.CrushSeconds;
            _instance.AttachVisual(offer);

            _instance._log.Write(
                $"Spawned location={offer.Decision.LocationId} "
                    + $"reward={Id(__instance)} slot={offer.Decision.AetherSlot} "
                    + $"crushSeconds={__instance.maxCrushSeconds:F2}"
            );
        });

    [HarmonyPatch(typeof(RewardInteractable), nameof(RewardInteractable.GrabStart))]
    [HarmonyPostfix]
    private static void HintOnGrab(RewardInteractable __instance) =>
        _instance.Safe(() =>
        {
            if (!_instance.TryGet(__instance, out var offer) || offer.HintSent || offer.Finished)
                return;

            offer.HintSent = true;
            _instance._client.HintStageReward(offer.Decision.LocationId);
            _instance._log.Write($"Grabbed location={offer.Decision.LocationId} hint=requested");
        });

    [HarmonyPatch(typeof(RewardInteractable), "HandleOtherRewardCrushed")]
    [HarmonyPrefix]
    private static void Decline(RewardInteractable __instance) =>
        _instance.Safe(() =>
        {
            if (!_instance.TryGet(__instance, out var offer) || offer.Finished)
                return;

            offer.Finished = true;
            offer.Visual?.Dispose();
            offer.Visual = null;
            _instance._client.DeclineStageReward(offer.Decision.LocationId);
            _instance._log.Write(
                $"Declined, different reward chosen location={offer.Decision.LocationId} visual=removed"
            );
        });

    [HarmonyPatch(typeof(RewardInteractable), nameof(RewardInteractable.OnCrushComplete))]
    [HarmonyPrefix]
    private static void BeginComplete(RewardInteractable __instance) =>
        _completingOffer = _instance.TryGet(__instance, out var offer) ? offer : null;

    [HarmonyPatch(typeof(RewardInteractable), nameof(RewardInteractable.OnCrushComplete))]
    [HarmonyPostfix]
    private static void Complete(RewardInteractable __instance) =>
        _instance.Safe(() =>
        {
            try
            {
                if (!_instance.TryGet(__instance, out var offer) || offer.Finished)
                    return;
                offer.Finished = true;
                if (_instance._client.CompleteStageReward(offer.Decision.LocationId))
                    _instance._log.Write($"Claim complete location={offer.Decision.LocationId} pity=reset");
            }
            finally
            {
                _completingOffer = null;
            }
        });

    [HarmonyPatch(typeof(RewardInteractable), nameof(RewardInteractable.OnCrushComplete))]
    [HarmonyFinalizer]
    private static Exception? EndComplete(Exception? __exception)
    {
        _completingOffer = null;

        return __exception;
    }

    [HarmonyPatch(typeof(Il2Cpp.RoomCompletionHistory), nameof(Il2Cpp.RoomCompletionHistory.SetCrushedReward))]
    [HarmonyPrefix]
    private static void UseHistoryPlaceholder(ref ArmamentAugment crushedRewardDef)
    {
        try
        {
            var offer = _completingOffer;
            if (offer == null)
                return;

            crushedRewardDef = offer.HistoryPlaceholder;
            _instance._log.Write(
                $"History cleared location={offer.Decision.LocationId} placeholder='{crushedRewardDef.DeveloperName}'"
            );
        }
        catch (Exception exception)
        {
            _instance._log.Write($"Runtime failed open type={exception.GetType().Name} message='{exception.Message}'");
        }
    }

    [HarmonyPatch(typeof(RewardInteractable), nameof(RewardInteractable.DisplayName), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool OverrideDisplayName(RewardInteractable __instance, ref string __result)
    {
        if (!_instance.TryGet(__instance, out var offer))
            return true;

        __result = $"<color=#{offer.Color}>{offer.Title}</color>";
        return false;
    }

    [HarmonyPatch(typeof(RewardInteractable), nameof(RewardInteractable.DisplayDescription), MethodType.Getter)]
    [HarmonyPrefix]
    private static bool OverrideDisplayDescription(RewardInteractable __instance, ref string __result)
    {
        if (!_instance.TryGet(__instance, out var offer))
            return true;

        __result = offer.Description;
        return false;
    }

    [HarmonyPatch(typeof(DescriptionInspectionPanel), nameof(DescriptionInspectionPanel.Populate))]
    [HarmonyPostfix]
    private static void CompactInspectionPanel(DescriptionInspectionPanel __instance, CrushInteractable __1) =>
        _instance.Safe(() =>
        {
            if (
                __1?.TryCast<RewardInteractable>() is not { } reward
                || !_instance.TryGet(reward, out _) && !_instance._nativeAetherRewards.Contains(Id(reward))
            )
                return;

            ApRewardInspectionPanel.Compact(__instance, _instance._log.Sink, "APStageReward");
        });

    private bool TryGet(RewardInteractable reward, out ActiveOffer offer) =>
        _rewardOffers.TryGetValue(Id(reward), out offer!);

    private void AttachVisual(ActiveOffer offer)
    {
        if (offer.Reward == null || offer.Visual != null)
            return;

        offer.Visual = ApLogoCrushableVisual.TryAttach(offer.Reward, _log.Sink);
        if (offer.Visual == null)
            offer.VisualRetryAt = Environment.TickCount64 + RuneforgeShopRules.VisualRetryDelayMs;
    }

    private void Safe(Action action) =>
        StageRewardDiagnosticGuard.Run(
            action,
            exception =>
                _log.Write($"Runtime failed open type={exception.GetType().Name} message='{exception.Message}'")
        );

    private void ClearRuntime()
    {
        var offers = _augmentOffers.Values.ToArray();
        _augmentOffers.Clear();
        _rewardOffers.Clear();
        _completingOffer = null;
        foreach (var offer in offers)
        {
            try
            {
                offer.Visual?.Dispose();
            }
            catch (Exception exception)
            {
                _log.Write($"Cleanup failed type={exception.GetType().Name} message='{exception.Message}'");
            }
        }
    }

    private static int Id(Object? value) => value == null ? 0 : value.GetInstanceID();

    public void Dispose() => Safe(ClearRuntime);
}
