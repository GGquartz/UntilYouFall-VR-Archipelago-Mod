using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;

namespace UntilYouFallArchipelago;

internal sealed record QuestRunState(
    bool Active = false,
    bool UsedVitality = false,
    bool UsedResolve = false,
    int StartingAether = 0,
    bool UsedAether = false,
    bool FinalHorrorCounted = false
);

internal sealed partial class ArchipelagoClient
{
    private const float SpeedrunSeconds = 25 * 60;
    private readonly Queue<Action> _unboundRunEvents = new();
    private bool _unboundRunActive;

    public int QuestProgress(int quest) =>
        _state != null && _state.QuestProgress.TryGetValue(quest, out var value) ? value : 0;

    public bool QuestDone(int quest) => QuestProgress(quest) >= QuestChecks.All[quest].Target;

    public bool QuestHinted(int quest) => _state != null && _state.QuestHinted.Contains(quest);

    public bool QuestRunActive => _state?.QuestRun.Active ?? _unboundRunActive;

    public string? QuestRewardText(long locationId)
    {
        if (
            !_stageRewardInfo.TryGetValue(locationId, out var info)
            || !QuestChecks.TryGetIndex(locationId, out var quest)
            || !(_questRewardHints.Contains(locationId) || QuestHinted(quest))
        )
            return null;

        var owner = info.IsLocalPlayer ? "Your" : StageRewardInspection.Safe(info.PlayerAlias) + "'s";
        return $"{ApColors.Rich(ApColors.Gold, owner)} {ApColors.Rich(StageRewardInspection.Color(info), StageRewardInspection.Safe(info.ItemName))}";
    }

    public enum QuestHintResult
    {
        Refused,
        NotEnoughAether,
        Hinted,
    }

    public QuestHintResult HintQuest(int quest)
    {
        if (!Connected || _state == null || !ActiveQuests.Contains(quest) || QuestDone(quest) || QuestHinted(quest))
            return QuestHintResult.Refused;

        var state = _state;
        if (QuestHintCost == 0)
            state.MarkQuestHinted(quest);
        else if (!AetherFiller.TrySpend(QuestHintCost, () => state.MarkQuestHinted(quest), _log.Sink))
            return QuestHintResult.NotEnoughAether;

        var ids = Enumerable
            .Range(1, QuestChecks.All[quest].Tier)
            .Select(reward => QuestChecks.LocationId(quest, reward))
            .ToArray();
        _questLog.Write(
            $"Hint bought quest={QuestChecks.All[quest].Name} aether={QuestHintCost} locations={string.Join(",", ids)}"
        );
        ScoutRewardLocations(HintCreationPolicy.CreateAndAnnounceOnce, ids, cacheResults: false);

        return QuestHintResult.Hinted;
    }

    private void TrackQuestHints(ArchipelagoSession session, int slot)
    {
        var generation = _connection.Generation;
        session.DataStorage.TrackHints(hints =>
        {
            var ids = hints
                .Where(hint => hint.FindingPlayer == slot && QuestChecks.TryGetIndex(hint.LocationId, out _))
                .Select(hint => hint.LocationId)
                .ToArray();
            if (ids.Length == 0)
                return;

            PostSessionEvent(
                generation,
                () =>
                {
                    foreach (var id in ids)
                        _questRewardHints.Add(id);
                }
            );
        });
    }

    public void QuestKill(EnemyCheck enemy, int stage = 0)
    {
        foreach (var source in QuestChecks.KillQuestSources(enemy.Name))
        {
            if (source == QuestSource.HorrorStages && stage == StageChecks.Count)
                CountFinalHorror();
            else
                AddQuestProgress(source, 1);
        }
    }

    public void QuestClear(int stage, IReadOnlyCollection<string> weaponNames)
    {
        if (stage == StageChecks.Count)
            CountFinalHorror();
        foreach (var index in ActiveQuests)
        {
            var quest = QuestChecks.All[index];
            if (QuestChecks.IsClearQuest(quest) && QuestChecks.ClearMatches(quest, stage, weaponNames))
                SetQuestProgress(index, quest.Target);
        }
    }

    private void CountFinalHorror()
    {
        if (!QuestRunActive)
            return;
        if (_state == null)
        {
            _unboundRunEvents.Enqueue(CountFinalHorror);
            return;
        }
        if (_state.QuestRun.FinalHorrorCounted)
            return;

        _state.SetQuestRun(_state.QuestRun with { FinalHorrorCounted = true });
        AddQuestProgress(QuestSource.HorrorStages, 1);
        _questEventsLog.Write("Final aether horror credited once for this run");
    }

    public bool QuestTracked(QuestSource source)
    {
        foreach (var index in ActiveQuests)
        {
            var quest = QuestChecks.All[index];
            if (quest.Source != source || QuestDone(index))
                continue;

            if (
                !StartingHands.HasValue
                || QuestChecks.UnlocksOwned(quest, WeaponUnlocks, WeaponAbilityUnlocks, SeparateWeaponSupers)
            )
                return true;
        }

        return false;
    }

    public void ObserveQuestProgress(QuestSource source, int value)
    {
        foreach (var index in ActiveQuests.Where(index => QuestChecks.All[index].Source == source && !QuestDone(index)))
            SetQuestProgress(index, value);
    }

    public void QuestAether(int aether)
    {
        if (!QuestRunActive || aether < 0)
            return;
        if (_state == null)
        {
            _unboundRunEvents.Enqueue(() => QuestAether(aether));
            return;
        }

        ObserveQuestProgress(QuestSource.AetherRun, aether);
        ObserveQuestProgress(
            QuestSource.AetherTotal,
            (int)Math.Min(int.MaxValue, (long)_state!.QuestRun.StartingAether + aether)
        );
    }

    public void RecordRewardKindsUsed(bool vitality, bool resolve, bool aether = false)
    {
        if (!QuestRunActive)
            return;
        if (_state == null)
        {
            _unboundRunEvents.Enqueue(() => RecordRewardKindsUsed(vitality, resolve, aether));
            return;
        }

        var run = _state!.QuestRun;
        _state.SetQuestRun(
            run with
            {
                UsedVitality = run.UsedVitality || vitality,
                UsedResolve = run.UsedResolve || resolve,
                UsedAether = run.UsedAether || aether,
            }
        );
    }

    public void QuestRunFinished(bool victory, float runSeconds, int tier = -1)
    {
        if (!QuestRunActive)
            return;
        if (_state == null)
        {
            _unboundRunActive = false;
            _unboundRunEvents.Enqueue(() => QuestRunFinished(victory, runSeconds, tier));
            return;
        }

        var run = _state!.QuestRun;
        if (victory)
        {
            var max = DifficultyConfig != null && tier >= DifficultyConfig.Maximum;
            _state.RecordGoalRun(max);
            _goalLog.Write(
                $"Run finished tier={tier} max={max} runs={_state.GoalRuns} maxRuns={_state.GoalMaxDifficultyRuns}"
            );
            if (runSeconds > 0f && runSeconds <= SpeedrunSeconds)
                ObserveQuestProgress(QuestSource.Speedrun, 1);
            if (!run.UsedVitality)
                ObserveQuestProgress(QuestSource.NoVitality, 1);
            if (!run.UsedResolve)
                ObserveQuestProgress(QuestSource.NoResolve, 1);
            if (!run.UsedAether)
                ObserveQuestProgress(QuestSource.NoAetherReward, 1);
        }
        _state.SetQuestRun(run with { Active = false });
        TrySendGoal();
    }

    public void DebugGoalRun()
    {
        if (_state == null || Goal == null)
            return;

        _state.RecordGoalRun(true);
        _goalLog.Write($"DEBUG run recorded runs={_state.GoalRuns} maxRuns={_state.GoalMaxDifficultyRuns}");
        TrySendGoal();
    }

    public void DebugCompleteQuests()
    {
        if (_state == null)
            return;

        foreach (var index in ActiveQuests)
            SetQuestProgress(index, QuestChecks.All[index].Target);
        _goalLog.Write($"DEBUG quests completed: {ActiveQuests.Count(QuestDone)}/{ActiveQuests.Length}");
        TrySendGoal();
    }

    public int QuestSuperUsed(string name)
    {
        if (_state == null)
            return 0;

        _state.MarkSuperUsed(name);
        ObserveQuestProgress(QuestSource.AllSupers, _state.SupersUsed.Count);

        return _state.SupersUsed.Count;
    }

    public void QuestRunStarted()
    {
        if (_state == null)
        {
            _unboundRunActive = true;
            _unboundRunEvents.Enqueue(QuestRunStarted);
            return;
        }

        var total = ActiveQuests
            .Where(index => QuestChecks.All[index].Source == QuestSource.AetherTotal)
            .Select(QuestProgress)
            .DefaultIfEmpty(0)
            .Max();
        _state.SetQuestRun(new QuestRunState(Active: true, StartingAether: total));
    }

    public void QuestWeaponsChanged()
    {
        if (_state == null)
            return;

        foreach (var index in ActiveQuests.Where(index => QuestChecks.All[index].Source == QuestSource.WeaponsAcquired))
            SetQuestProgress(index, _state.WeaponUnlocks.Count);
    }

    public void AddQuestProgress(QuestSource source, int amount)
    {
        foreach (var index in ActiveQuests.Where(index => QuestChecks.All[index].Source == source && !QuestDone(index)))
            SetQuestProgress(index, QuestProgress(index) + amount);
    }

    private void SetQuestProgress(int index, int value)
    {
        var quest = QuestChecks.All[index];
        value = Math.Min(value, quest.Target);
        if (_state == null)
            return;

        value = Math.Max(value, QuestProgress(index));
        if (value > QuestProgress(index))
        {
            _state.SetQuestProgress(index, value);
            _questLog.Write($"{quest.Name}: {value}/{quest.Target}");
        }
        if (value < quest.Target)
            return;

        for (var reward = 1; reward <= quest.Tier; reward++)
            RecordCheck(QuestChecks.LocationId(index, reward), QuestChecks.Name(index, reward), "APQuest");
    }
}
