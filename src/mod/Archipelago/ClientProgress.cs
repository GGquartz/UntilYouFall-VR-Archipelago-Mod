namespace UntilYouFallArchipelago;

internal sealed class ClientProgress
{
    private readonly ClientConnection _connection;
    private readonly LogChannel _log;
    private readonly LogChannel _goalLog;
    private readonly HashSet<long> _confirmedChecks = [];
    private ApState? _state;
    private ClientSlotSettings? _settings;
    private bool _goalSent;
    public long[] CheckIds { get; set; } = ArchipelagoClient.BaseCheckIds;
    public int ConfirmedChecks => CheckIds.Count(_confirmedChecks.Contains);
    public IReadOnlySet<long> ConfirmedLocationIds => _confirmedChecks;

    public ClientProgress(ClientConnection connection, Action<string> log)
    {
        _connection = connection;
        _log = log.Channel("AP");
        _goalLog = log.Channel("APGoal");
    }

    public void Initialize(ApState state, ClientSlotSettings settings)
    {
        _state = state;
        _settings = settings;
        _confirmedChecks.Clear();
    }

    public void ResetGoalSent() => _goalSent = false;

    public bool IsChecked(long locationId) =>
        _state != null && (_state.Checks.Contains(locationId) || _confirmedChecks.Contains(locationId));

    public void HandleCheckedLocations(long[] ids, bool report)
    {
        _confirmedChecks.UnionWith(ids);
        if (report)
            _log.Write($"Server confirmed checks={ConfirmedChecks}/{CheckIds.Length}");
        TrySendGoal();
    }

    public void RecordCheck(long id, string name, string tag)
    {
        if (_state == null)
        {
            _log.Sink.Write(tag, $"{name} ignored: connect before collecting checks");
            return;
        }
        if (IsChecked(id))
            return;

        _state.RecordCheck(id);
        _log.Sink.Write(tag, $"{name} recorded locally id={id} connected={_connection.Connected}");
        SendChecks([id]);
        TrySendGoal();
    }

    public void SendChecks(IEnumerable<long> checks)
    {
        if (!_connection.Connected)
            return;
        var ids = checks.Where(id => !_confirmedChecks.Contains(id)).ToArray();
        if (ids.Length == 0)
            return;

        var session = _connection.Session!;
        var generation = _connection.Generation;
        _ = Task.Run(async () =>
        {
            try
            {
                await session.Locations.CompleteLocationChecksAsync(ids);
            }
            catch (Exception ex)
            {
                _connection.Post(
                    generation,
                    () => _log.Write("Check send failed - saved for reconnect: " + ex.Message)
                );
            }
        });
    }

    private int FinalClearedWeapons() =>
        WeaponCatalog.All.Count(weapon => IsChecked(StageChecks.LocationId(StageChecks.Count, weapon.Id)));

    public string? WeaponStageProgress(long weaponId) =>
        _state == null || WeaponCatalog.ById(weaponId) == null
            ? null
            : StageProgress(stage => StageChecks.LocationId(stage, weaponId));

    public string? DifficultyStageProgress(int tier) =>
        _state == null || _settings == null || !StageDifficultyChecks.IsActive(_settings.Difficulty, tier)
            ? null
            : StageProgress(
                stage => StageDifficultyChecks.LocationId(stage, tier),
                stage => !StageDifficultyChecks.IsGoalCheck(_settings.Difficulty, _settings.Goal, stage, tier)
            );

    private string StageProgress(Func<int, long> locationId, Func<int, bool>? exists = null)
    {
        var stages = Enumerable.Range(1, StageChecks.Count).Where(stage => exists == null || exists(stage)).ToArray();
        var count = stages.Count(stage => IsChecked(locationId(stage)));

        return $"Stages Cleared {count}/{stages.Length}";
    }

    private bool QuestDone(int quest) =>
        (_state!.QuestProgress.TryGetValue(quest, out var value) ? value : 0) >= QuestChecks.All[quest].Target;

    public string? GoalProgressText() =>
        _settings == null || _state == null
            ? null
            : GoalRules.GetProgressText(
                _settings.Goal,
                _settings.ActiveQuests.Count(QuestDone),
                _settings.ActiveQuests.Length,
                FinalClearedWeapons(),
                WeaponCatalog.All.Length,
                _state.GoalRuns,
                _state.GoalMaxDifficultyRuns,
                DifficultySettings.Names[_settings.Difficulty.Maximum]
            );

    public void TrySendGoal()
    {
        if (!_connection.Connected || _goalSent || _state == null || _settings == null)
            return;

        var goal = _settings.Goal;
        var allQuestsDone = _settings.ActiveQuests.All(QuestDone);
        var allWeaponsCleared = FinalClearedWeapons() == WeaponCatalog.All.Length;
        var complete = GoalRules.IsComplete(
            goal,
            allQuestsDone,
            allWeaponsCleared,
            _state.GoalRuns,
            _state.GoalMaxDifficultyRuns
        );
        if (!GoalRules.ShouldSend(_connection.Connected, _goalSent, complete))
            return;

        _goalSent = true;
        _connection.Session!.SetGoalAchieved();
        _goalLog.Write(
            $"Goal sent mode={goal.Mode} target={goal.Runs} runs={_state.GoalRuns} maxRuns={_state.GoalMaxDifficultyRuns}"
        );
    }
}
