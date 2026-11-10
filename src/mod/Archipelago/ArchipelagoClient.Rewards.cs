using System.Collections.Concurrent;
using System.Text.Json;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using UntilYouFall.Shared;

namespace UntilYouFallArchipelago;

internal sealed partial class ArchipelagoClient
{
    internal void RecordRunOnlyEffectReceipt(int index, long item, bool? canReceiveRunOnlyEffects = null)
    {
        if (ApItems.IsRunOnlyItem(item))
        {
            _runOnlyEffectReceipts.TryAdd(
                index,
                canReceiveRunOnlyEffects ?? _canReceiveRunOnlyEffects?.Invoke() == true
            );
            if (!_runOnlyEffectReceipts[index])
                _state?.VoidRunOnlyEffect(index);
        }
    }

    public void VoidRunOnlyEffects()
    {
        ++_runOnlyEffectEpoch;
        foreach (var index in _runOnlyEffectReceipts.Keys.ToArray())
        {
            _runOnlyEffectReceipts[index] = false;
            try
            {
                _state?.VoidRunOnlyEffect(index);
            }
            catch (Exception exception)
            {
                _log.Write("Failed to persist voided run only effect, disconnecting: " + exception.Message);
                Disconnect();

                return;
            }
        }
    }

    public bool TryGetStageRewardInfo(long locationId, out StageRewardItemInfo info) =>
        _stageRewardInfo.TryGetValue(locationId, out info!);

    public StageRewardOfferDecision? PrepareStageRewardOffer(
        string room,
        IReadOnlyList<bool> aetherSlots,
        int chanceRoll,
        int slotRoll,
        bool forceOffer
    )
    {
        if (!Connected || _state == null || StageRewardCheckCount < StageRewardChecks.MinimumCount)
            return null;
        if (!StageRewardRuntimeRules.CanOfferInRoom(room, _state.LastCompletedStageRewardRoom))
        {
            _stageRewardLog.Write($"No offer in completed room room='{room}'");
            return null;
        }
        var locationId = StageRewardProgress.NextLocationId(
            StageRewardCheckCount,
            _state.Checks,
            _progress.ConfirmedLocationIds
        );
        if (locationId == null || !TryGetStageRewardInfo(locationId.Value, out _))
            return null;

        var savedMatches = room == _state.StageRewardOfferRoom && locationId == _state.StageRewardOfferLocationId;
        if (savedMatches)
        {
            var reusedSlot = StageRewardRuntimeRules.ReusableSlot(
                room,
                locationId.Value,
                _state.StageRewardOfferRoom,
                _state.StageRewardOfferLocationId,
                _state.StageRewardOfferSlot,
                aetherSlots,
                slotRoll
            );
            if (reusedSlot == null)
                return null;

            if (_state.StageRewardOfferSlot != reusedSlot)
                _state.SetStageRewardOffer(room, locationId.Value, reusedSlot.Value);
            return new StageRewardOfferDecision(
                locationId.Value,
                Math.Max(1, _state.StageRewardPity),
                reusedSlot.Value
            );
        }

        if (_state.StageRewardOfferRoom != null)
            _state.ClearStageRewardOffer();
        var aetherCount = aetherSlots.Count(value => value);
        if (aetherCount == 0)
            return null;

        var attempt = _state.IncrementStageRewardPity(room);
        if (attempt == null)
            return null;

        var checkIndex = (int)(locationId.Value - StageRewardChecks.BaseId);
        var offered = StageRewardOfferRules.ShouldOffer(
            connected: Connected,
            hasNextCheck: true,
            scoutReady: true,
            aetherRewardCount: aetherCount,
            roll: chanceRoll,
            eligibleAttempt: attempt.Value,
            checkIndex: checkIndex,
            activeCount: StageRewardCheckCount,
            forceOffer: forceOffer
        );
        _stageRewardLog.Write(
            $"Attempt location={locationId.Value} attempt={attempt.Value} "
                + $"chance={StageRewardProgress.ChancePercent(attempt.Value, checkIndex, StageRewardCheckCount)}% "
                + $"forced={forceOffer} offered={offered}"
        );
        if (!offered)
            return null;

        var slot = StageRewardOfferRules.SelectAetherSlot(aetherSlots, slotRoll);
        if (slot == null)
            return null;

        _state.SetStageRewardOffer(room, locationId.Value, slot.Value);
        return new StageRewardOfferDecision(locationId.Value, attempt.Value, slot.Value);
    }

    public void DeclineStageReward(long locationId)
    {
        if (_state?.StageRewardOfferLocationId != locationId)
            return;

        _state.ClearStageRewardOffer();
        _stageRewardLog.Write(
            $"Offered but did not pick stage reward location={StageRewardChecks.Name((int)(locationId - StageRewardChecks.BaseId))} id={locationId} pity={_state.StageRewardPity}"
        );
    }

    public bool CompleteStageReward(long locationId)
    {
        if (_state == null || !_state.CompleteStageReward(locationId))
            return false;

        var name = StageRewardChecks.Name((int)(locationId - StageRewardChecks.BaseId));
        _stageRewardLog.Write($"{name} recorded locally id={locationId} connected={Connected}");
        SendChecks([locationId]);

        return true;
    }

    public void HintStageReward(long locationId)
    {
        if (!Connected || !_stageRewardInfo.ContainsKey(locationId) || !_hintedStageRewards.Add(locationId))
            return;

        ScoutRewardLocations(HintCreationPolicy.CreateAndAnnounceOnce, [locationId], cacheResults: false);
    }

    private void ScoutRewardLocations(HintCreationPolicy policy, long[] locationIds, bool cacheResults)
    {
        if (_connection.Session == null || locationIds.Length == 0)
            return;

        var session = _connection.Session;
        var generation = _connection.Generation;
        var gate = _stageRewardScoutGate;
        _ = Task.Run(async () =>
        {
            await gate.WaitAsync();
            try
            {
                if (generation != _connection.Generation)
                    return;

                var scouted = await session.Locations.ScoutLocationsAsync(policy, locationIds);
                var converted = scouted.Values.Select(StageRewardItemInfo.From).ToArray();
                PostSessionEvent(
                    generation,
                    () =>
                    {
                        if (cacheResults)
                            foreach (var info in converted)
                                _stageRewardInfo[info.LocationId] = info;
                    }
                );
            }
            catch (Exception exception)
            {
                PostSessionEvent(
                    generation,
                    () =>
                    {
                        if (!cacheResults)
                            foreach (var id in locationIds)
                                _hintedStageRewards.Remove(id);
                        _log.Write("Stage reward scout failed: " + exception.Message);
                    }
                );
            }
            finally
            {
                gate.Release();
            }
        });
    }
}
