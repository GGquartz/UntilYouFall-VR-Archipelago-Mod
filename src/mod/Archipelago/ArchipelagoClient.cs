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
    public const string Game = GameIdentity.Name;
    public static readonly long[] BaseCheckIds = Enumerable
        .Range(1, StageChecks.Count)
        .SelectMany(stage => WeaponCatalog.All.Select(weapon => StageChecks.LocationId(stage, weapon.Id)))
        .Concat(EnemyChecks.All.Select(enemy => enemy.Id))
        .ToArray();
    private readonly string _stateDirectory;
    private readonly LogChannel _log;
    private readonly LogChannel _stateLog;
    private readonly LogChannel _questLog;
    private readonly LogChannel _eliteTrapLog;
    private readonly LogChannel _difficultyStageLog;
    private readonly LogChannel _goalLog;
    private readonly LogChannel _shopLog;
    private readonly LogChannel _difficultyLog;
    private readonly LogChannel _stageRewardLog;
    private readonly LogChannel _questEventsLog;
    private readonly LogChannel _startingTraitsLog;
    private readonly LogChannel _weaponHintLog;
    private readonly ClientNotifications _notifications;
    private readonly Action<ApConsoleMessage>? _console;
    private readonly ClientConnection _connection;
    private readonly ClientProgress _progress;
    private readonly SortedDictionary<int, long> _pendingItems = [];
    private readonly Dictionary<int, bool> _runOnlyEffectReceipts = [];
    private int _runOnlyEffectEpoch;
    private readonly Func<bool>? _canReceiveRunOnlyEffects;
    private readonly ReceivedItemHistory _receivedItemHistory = new();
    private readonly Dictionary<long, StageRewardItemInfo> _stageRewardInfo = [];
    private readonly HashSet<long> _hintedStageRewards = [];
    private readonly HashSet<long> _questRewardHints = [];
    private SemaphoreSlim _stageRewardScoutGate = new(1, 1);
    private static readonly IReadOnlyDictionary<long, int> EmptyPlayerBonusCounts = new Dictionary<long, int>();
    private ApState? _state;
    private ClientSlotSettings? _settings;
    public bool Connected => _connection.Connected;
    public int HandledCount => _state?.HandledCount ?? 0;
    public int ConfirmedChecks => _progress.ConfirmedChecks;
    public long[] CheckIds => _progress.CheckIds;
    public int[] ActiveQuests => _settings?.ActiveQuests ?? Array.Empty<int>();
    public int QuestHintCost { get; private set; } = QuestChecks.DefaultHintCost;
    public GoalSettings? Goal => _settings?.Goal;
    public int PendingItems => _pendingItems.Keys.Count(index => index >= HandledCount);
    public string? PendingEliteEncounterTrap => Connected ? _state?.PendingEliteEncounterTrap : null;
    public IReadOnlyCollection<long> WeaponUnlocks =>
        (IReadOnlyCollection<long>?)_state?.WeaponUnlocks ?? Array.Empty<long>();
    public IReadOnlyCollection<long> HandUnlocks =>
        (IReadOnlyCollection<long>?)_state?.HandUnlocks ?? Array.Empty<long>();
    public IReadOnlyCollection<long> TraitSlotUnlocks =>
        (IReadOnlyCollection<long>?)_state?.TraitSlotUnlocks ?? Array.Empty<long>();
    public IReadOnlyCollection<long> WeaponAbilityUnlocks =>
        (IReadOnlyCollection<long>?)_state?.WeaponAbilityUnlocks ?? Array.Empty<long>();
    public bool SeparateWeaponSupers { get; private set; }
    public DifficultySettings? DifficultyConfig => _settings?.Difficulty;
    public IReadOnlyCollection<long> DifficultyUnlocks =>
        (IReadOnlyCollection<long>?)_state?.DifficultyUnlocks ?? Array.Empty<long>();
    public int? WeaponUpgradeItemCount => _state?.WeaponUpgradeItemCount;
    public int ShopUnlockCount =>
        Math.Min(_receivedItemHistory.Count(ApItems.ProgressiveShopUnlockId), ShopChecks.Sections - 1);
    public int AetherMultiplierItemCount => _state?.AetherMultiplierItemCount ?? 0;
    public IReadOnlyDictionary<long, int> PlayerBonusItemCounts =>
        _state?.PlayerBonusItemCounts ?? EmptyPlayerBonusCounts;
    public IReadOnlyList<long> StartingWeaponIds { get; private set; } = Array.Empty<long>();
    public StartingHands? StartingHands { get; private set; }
    public int StageRewardCheckCount { get; private set; }
    public DeathLinkMode DeathLinkMode { get; private set; } = DeathLinkMode.Off;
    public delegate void DeathLinkHandler(string source, string cause);

    public DeathLinkHandler? DeathLinkReceived { get; set; }
    private DeathLinkService? _deathLink;
    public int TrapEffectLengthSeconds { get; private set; } = TimedTrapState.DefaultDurationSeconds;

    public ArchipelagoClient(
        string stateDirectory,
        Action<string> log,
        Action<ApNotice>? notify = null,
        Action<ApConsoleMessage>? console = null,
        Func<bool>? canReceiveRunOnlyEffects = null
    )
    {
        _stateDirectory = stateDirectory;
        _log = log.Channel("AP");
        _stateLog = log.Channel("APState");
        _questLog = log.Channel("APQuest");
        _eliteTrapLog = log.Channel("APEliteTrap");
        _difficultyStageLog = log.Channel("APDifficultyStage");
        _goalLog = log.Channel("APGoal");
        _shopLog = log.Channel("APShop");
        _difficultyLog = log.Channel("APDifficulty");
        _stageRewardLog = log.Channel("APStageReward");
        _questEventsLog = log.Channel("APQuestEvents");
        _startingTraitsLog = log.Channel("APStartingTraits");
        _weaponHintLog = log.Channel("APWeaponHint");
        _notifications = new ClientNotifications(log, notify);
        _connection = new ClientConnection(log, Report, Disconnect);
        _progress = new ClientProgress(_connection, log);
        _console = console;
        _canReceiveRunOnlyEffects = canReceiveRunOnlyEffects;
    }

    public Action<ConnectionState, string>? StatusChanged { get; set; }
    public Action? ConnectionLost { get; set; }

    private void Report(ConnectionState state, string detail) => StatusChanged?.Invoke(state, detail);

    public void Connect(string server, string slot, string password)
    {
        LauncherProtocol.ConnectCommand(server, slot, password);

        var endpoint = LauncherProtocol.ServerEndpoint(server);
        var address = server.Contains("://") ? endpoint : server.TrimEnd('/');
        if (_connection.Session != null)
            Disconnect();

        var attempt = _connection.Begin(address, slot);
        _progress.ResetGoalSent();
        WeaponHintsEnabled = false;
        ResetWeaponHintRequest();
        _stageRewardScoutGate = new SemaphoreSlim(1, 1);
        RegisterSessionHandlers(attempt.Session, attempt.Generation, attempt.PendingEvents);

        _log.Write($"Connecting to {address} slot={slot}");
        _ = Task.Run(() =>
            _connection.Login(attempt, password, (success, seed) => InitializeSession(attempt, success, seed))
        );
    }

    internal void PostSessionEvent(int generation, Action action) => _connection.Post(generation, action);

    internal void PostSessionData(int generation, Queue<Action> pendingSessionEvents, Action<bool> action) =>
        PostSessionEvent(
            generation,
            () =>
            {
                var runOnlyEffectEpoch = _runOnlyEffectEpoch;
                var canReceiveRunOnlyEffects = _canReceiveRunOnlyEffects?.Invoke() == true;
                if (_connection.LoggedIn)
                    action(canReceiveRunOnlyEffects);
                else
                    pendingSessionEvents.Enqueue(() =>
                        action(canReceiveRunOnlyEffects && runOnlyEffectEpoch == _runOnlyEffectEpoch)
                    );
            }
        );

    private void RegisterSessionHandlers(ArchipelagoSession session, int generation, Queue<Action> pendingSessionEvents)
    {
        void Post(Action action) => PostSessionEvent(generation, action);
        void PostData(Action<bool> action) => PostSessionData(generation, pendingSessionEvents, action);

        session.Socket.PacketReceived += packet =>
        {
            if (packet is ReceivedItemsPacket items)
            {
                var start = items.Index;
                var received = items.Items.ToArray();

                PostData(canReceiveRunOnlyEffects => HandleReceivedItems(start, received, canReceiveRunOnlyEffects));
            }
            else if (packet is ConnectedPacket connected)
            {
                var ids = connected.LocationsChecked.ToArray();
                PostData(_ =>
                {
                    HandleCheckedLocations(ids, report: false);
                });
            }
            else if (packet is RoomUpdatePacket update && update.CheckedLocations != null)
            {
                var ids = update.CheckedLocations.ToArray();
                PostData(_ =>
                {
                    HandleCheckedLocations(ids, report: true);
                });
            }
        };

        session.MessageLog.OnMessageReceived += message => HandleServerMessage(session, generation, message);
        session.Socket.ErrorReceived += (_, message) => Post(() => _log.Write("Socket error: " + message));
        session.Socket.SocketClosed += reason => Post(() => HandleSocketClosed(reason));
    }

    internal void HandleReceivedItems(int start, NetworkItem[] received, bool canReceiveRunOnlyEffects)
    {
        if (
            _connection.LoggedIn
            && _state != null
            && start == 0
            && (received.Length < _state.HandledCount || received.Length <= _state.LastEliteEncounterTrapIndex)
        )
        {
            _state.ResetItemHistory();
            _pendingItems.Clear();
            _runOnlyEffectReceipts.Clear();
            _notifications.Reset();
            _receivedItemHistory.Reset(preserveShopUnlocks: false);

            foreach (var starter in StartingWeaponIds)
                _state.UnlockWeapon(starter);
            if (DifficultyConfig != null)
                _state.UnlockDifficulty(DifficultySettings.ItemId(DifficultyConfig.Starting));

            _log.Write("Server item history is shorter than local - reset item cursor");
        }
        if (start == 0)
            _receivedItemHistory.ResetShopUnlocks();
        for (var i = 0; i < received.Length; i++)
        {
            var index = start + i;
            var item = received[i].Item;
            var receivedNewCopy = _receivedItemHistory.Record(index, item);
            if (_connection.LoggedIn)
            {
                SynchronizeDurableItem(item, updateCounts: receivedNewCopy);
                if (WeaponCatalog.ById(item) != null)
                    QuestWeaponsChanged();
            }
            if (_state == null || index >= _state.NotifiedReceivedCount)
                _notifications.QueueReceived(index, received[i]);
            if (_state != null && _connection.LoggedIn && index < _state.HandledCount)
                continue;
            if (_pendingItems.TryGetValue(index, out var previous) && previous != received[i].Item)
                throw new InvalidDataException("Conflicting AP item history");

            _pendingItems[index] = received[i].Item;
            RecordRunOnlyEffectReceipt(index, received[i].Item, canReceiveRunOnlyEffects);
        }

        _log.Write($"ReceivedItems start={start} count={received.Length}");
    }

    private void HandleCheckedLocations(long[] ids, bool report)
    {
        _progress.HandleCheckedLocations(ids, report);
    }

    private void HandleServerMessage(ArchipelagoSession session, int generation, LogMessage message)
    {
        void Post(Action action) => PostSessionEvent(generation, action);

        if (WeaponHintPending)
            Post(() => ReceiveWeaponHint(message));
        if (_console != null)
        {
            var entry = ApConsoleMessage.FromLog(message);
            Post(() => _console(entry));
        }
        if (message.GetType() != typeof(ItemSendLogMessage))
            return;
        var sent = (ItemSendLogMessage)message;
        if (!sent.IsSenderTheActivePlayer || sent.IsReceiverTheActivePlayer)
            return;
        var location = sent.Item.LocationId;
        var notice = ApNotice
            .Sent(sent.Item.ItemDisplayName, sent.Item.Flags, sent.Receiver.ToString())
            .AtLocation(session.Locations.GetLocationNameFromId(location) ?? $"Location: {location}");
        Post(() =>
        {
            if (_connection.LoggedIn)
                _notifications.NotifySent(location, notice);
        });
    }

    private void HandleSocketClosed(string reason)
    {
        var wasLoggedIn = _connection.LoggedIn;
        _connection.MarkSocketClosed();
        _stageRewardInfo.Clear();
        _hintedStageRewards.Clear();
        if (wasLoggedIn)
            ConnectionLost?.Invoke();

        _log.Write("Disconnected: " + reason + "- connect again to retry");
        Report(ConnectionState.Error, "Disconnected: " + reason);
    }

    private void InitializeSession(ClientConnection.Attempt attempt, LoginSuccessful success, string seed)
    {
        var session = attempt.Session;
        void Post(Action action) => PostSessionEvent(attempt.Generation, action);

        var settings = ClientSlotSettings.Read(success.SlotData);
        var identity = JsonSerializer.Serialize(
            new
            {
                seed,
                success.Team,
                success.Slot,
            }
        );
        var state = ApState.Open(_stateDirectory, identity);
        _stateLog.Write(
            $"Restored session seed={seed} team={success.Team} slot={success.Slot} handled={state.HandledCount} notices={state.NotifiedReceivedCount} checks={state.Checks.Count}"
        );
        if (_state?.Identity != state.Identity)
            _receivedItemHistory.ResetShopUnlocks();
        _state = state;
        _pendingItems.Clear();
        _runOnlyEffectReceipts.Clear();
        _notifications.Reset();
        _progress.Initialize(state, settings);
        _receivedItemHistory.Reset(preserveShopUnlocks: true);
        _stageRewardInfo.Clear();
        _hintedStageRewards.Clear();
        _questRewardHints.Clear();
        StartingWeaponIds = settings.StartingWeapons;
        StartingHands = settings.StartingHands;
        _settings = settings;
        QuestHintCost = settings.QuestHintCost;
        _questLog.Write($"Hint cost loaded aether={QuestHintCost}");
        SeparateWeaponSupers = settings.SeparateWeaponSupers;
        WeaponHintsEnabled = settings.WeaponHintsEnabled;
        StageRewardCheckCount = settings.StageRewardCheckCount;
        TrapEffectLengthSeconds = settings.TrapEffectLengthSeconds;
        DeathLinkMode = settings.DeathLinkMode;
        if (DeathLinkMode != DeathLinkMode.Off)
        {
            var alias = session.Players.ActivePlayer.Alias;
            var service = session.CreateDeathLinkService();
            service.OnDeathLinkReceived += link =>
            {
                if (link.Source == alias)
                    return;
                Post(() => DeathLinkReceived?.Invoke(link.Source, link.Cause ?? ""));
            };
            service.EnableDeathLink();
            _deathLink = service;
        }
        _progress.CheckIds = BaseCheckIds
            .Concat(StageRewardChecks.ActiveIds(StageRewardCheckCount))
            .Concat(StageDifficultyChecks.ActiveIds(settings.Difficulty, settings.Goal))
            .Concat(ShopChecks.AllIds())
            .Concat(QuestChecks.Ids(ActiveQuests))
            .ToArray();
        foreach (var starter in StartingWeaponIds)
            _state.UnlockWeapon(starter);
        _state.UnlockDifficulty(DifficultySettings.ItemId(settings.Difficulty.Starting));
        _connection.CompleteLogin(attempt);
        while (_unboundRunEvents.TryDequeue(out var apply))
            apply();
        QuestWeaponsChanged();
        foreach (var index in _pendingItems.Keys.Where(index => index < _state.HandledCount).ToArray())
            _pendingItems.Remove(index);
        _notifications.PruneReceived(_state.NotifiedReceivedCount);
        var scoutedIds = StageRewardChecks
            .ActiveIds(StageRewardCheckCount)
            .Concat(ShopChecks.AllIds())
            .Concat(QuestChecks.Ids(ActiveQuests))
            .ToArray();
        ScoutRewardLocations(HintCreationPolicy.None, scoutedIds, cacheResults: true);
        TrackQuestHints(session, success.Slot);

        _log.Write(
            $"Connected seed={seed} team={success.Team} slot={success.Slot} handled={HandledCount} confirmed={ConfirmedChecks}/{CheckIds.Length}"
        );
        Report(ConnectionState.Connected, $"Connected to {attempt.Address} as {attempt.Slot}");
        SendChecks(_state.Checks);
        TrySendGoal();
    }

    public void Update()
    {
        _connection.ProcessEvents();
        UpdateWeaponHint();
        RegisterPendingEliteTraps();
        if (Connected && _state != null)
            _notifications.DeliverReceived(_connection.Session!, _state, Disconnect);
    }

    private void RegisterPendingEliteTraps()
    {
        if (!Connected || _state == null)
            return;

        try
        {
            foreach (var receipt in _pendingItems)
            {
                if (receipt.Value != ApItems.EliteEncounterTrapId)
                    continue;
                var alreadyPending = _state.PendingEliteEncounterTrap != null;
                if (_state.RegisterEliteEncounterTrap(receipt.Key))
                    _eliteTrapLog.Write(
                        $"Item registered index={receipt.Key} coalesced={alreadyPending} pending={_state.PendingEliteEncounterTrap}"
                    );
            }
        }
        catch (Exception exception)
        {
            _log.Write("Cannot persist Elite Encounter Trap: " + exception.Message);
            Disconnect();
        }
    }

    public void ClearStage(int stage, IEnumerable<WeaponItem> weapons)
    {
        foreach (var weapon in weapons)
            RecordCheck(StageChecks.LocationId(stage, weapon.Id), StageChecks.Name(stage, weapon.Name), "APStage");
    }

    public void ClearStageDifficulty(int stage, int tier)
    {
        var settings = DifficultyConfig;
        if (settings == null)
            return;

        var active = StageDifficultyChecks.IsActive(settings, tier);
        var owned = settings.InRange(tier) && DifficultyUnlocks.Contains(DifficultySettings.ItemId(tier));
        _difficultyStageLog.Write(
            $"Clear stage={stage} tier={tier} starting={settings.Starting} max={settings.Maximum} active={active} owned={owned}"
        );
        if (active && owned && !StageDifficultyChecks.IsGoalCheck(settings, Goal, stage, tier))
            RecordCheck(
                StageDifficultyChecks.LocationId(stage, tier),
                StageDifficultyChecks.Name(stage, tier),
                "APDifficultyStage"
            );
    }

    public bool IsShopItemBought(long locationId) => _progress.IsChecked(locationId);

    public void BuyShopItem(int section, int slot) =>
        RecordCheck(ShopChecks.LocationId(section, slot), ShopChecks.Name(section, slot), "APShop");

    public void FirstKill(long enemyLocationId)
    {
        var enemy = EnemyChecks.ById(enemyLocationId) ?? throw new ArgumentOutOfRangeException(nameof(enemyLocationId));
        RecordCheck(enemy.Id, enemy.LocationName, "APEnemy");
    }

    private void RecordCheck(long id, string name, string tag) => _progress.RecordCheck(id, name, tag);

    public string PlayerAlias => _connection.Session?.Players.ActivePlayer.Alias ?? "";

    public bool SendDeathLink(string cause)
    {
        if (!Connected || _deathLink == null)
            return false;

        _deathLink.SendDeathLink(new DeathLink(PlayerAlias, cause));

        return true;
    }

    private void Notify(ApNotice notice) => _notifications.Notify(notice);

    private void SendChecks(IEnumerable<long> checks) => _progress.SendChecks(checks);

    public string? WeaponStageProgress(long weaponId) => _progress.WeaponStageProgress(weaponId);

    public string? DifficultyStageProgress(int tier) => _progress.DifficultyStageProgress(tier);

    public string? GoalProgressText() => _progress.GoalProgressText();

    public void TrySendGoal() => _progress.TrySendGoal();

    public void RegisterDeathLinkEliteTrap()
    {
        if (_state == null)
            return;

        try
        {
            var registered = _state.RegisterDeathLinkEliteTrap();
            _eliteTrapLog.Write(
                $"Trap triggered by death link received registered={registered} pending={_state.PendingEliteEncounterTrap}"
            );
        }
        catch (Exception exception)
        {
            _log.Write("Cannot persist elite encounter trap triggered by death link: " + exception.Message);
            Disconnect();
        }
    }

    public bool CompleteEliteEncounterTrap(string receipt)
    {
        if (_state == null)
            return false;

        try
        {
            if (!_state.CompleteEliteEncounterTrap(receipt))
                return false;

            _eliteTrapLog.Write($"Pending item completed receipt={receipt}");
            return true;
        }
        catch (Exception exception)
        {
            _log.Write("Cannot persist elite encounter trap completion: " + exception.Message);
            Disconnect();
            return false;
        }
    }

    public void Disconnect()
    {
        _connection.Disconnect();
        WeaponHintsEnabled = false;
        ResetWeaponHintRequest();
        _pendingItems.Clear();
        _notifications.ClearReceived();
        _stageRewardInfo.Clear();
        _hintedStageRewards.Clear();
        _questRewardHints.Clear();
        _deathLink = null;
        DeathLinkMode = DeathLinkMode.Off;

        _log.Write("Session closed, local state preserved");
    }
}
