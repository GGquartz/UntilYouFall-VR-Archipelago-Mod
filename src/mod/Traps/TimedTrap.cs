using Il2CppSG.Claymore.Entities;
using Il2CppSG.Claymore.Interaction.Rewards;
using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.UI;
using UnityEngine;

namespace UntilYouFallArchipelago;

internal abstract class TimedTrap
{
    protected readonly TimedTrapState _state;
    protected readonly Func<PlayerHealth?> _player;
    protected readonly Func<bool> _ready;
    protected readonly Func<LevelManager?> _level;
    protected readonly LogChannel _log;
    protected readonly TimedTrapHud _hud;
    private readonly Func<int> _durationSeconds;
    protected PlayerHealth? _owner;
    private RewardMomentController? _rewardMoment;
    private PauseScreen? _pauseScreen;
    private long _nextContextSearchAt;
    private bool _transitioning;
    private bool _wasPaused;

    protected TimedTrap(
        TimedTrapState state,
        string title,
        string tag,
        Func<int> row,
        Func<PlayerHealth?> player,
        Func<bool> ready,
        Func<LevelManager?> level,
        Func<int> durationSeconds,
        Action<string> log,
        Color? color = null
    )
    {
        _state = state;
        _player = player;
        _ready = ready;
        _level = level;
        _log = log.Channel(tag);
        _durationSeconds = durationSeconds;
        _hud = new TimedTrapHud(title, row, log, color);
    }

    public bool Active => _state.Active;

    protected virtual bool Activate(int durationSeconds) => _state.Activate(_ready(), durationSeconds);

    protected virtual void OnActivated() { }

    protected virtual void OnEnded() { }

    public void Receive()
    {
        if (!_state.InRun || !_ready())
        {
            _log.Write("Receive rejected");
            return;
        }
        try
        {
            var owner = _player();
            if (owner == null)
                return;
            var resetting = _state.Active;
            if (!Activate(_durationSeconds()))
                return;

            _owner = owner;
            _wasPaused = true;
            OnActivated();
            var manager = _level();

            _log.Write(
                $"{(resetting ? "Timer reset" : "Activated")} remaining={_state.RemainingSeconds} duration={_state.DurationSeconds} player={owner.Pointer} seed={manager?.CurrentSeed} stage={manager?.LevelIndex} no additive stacking."
            );
        }
        catch (Exception exception)
        {
            if (_state.Active)
                EndStage("activation-error");
            _log.Write($"Receive failed, no pending effect {exception}");
        }
    }

    public void Update()
    {
        if (!_state.Active)
            return;
        try
        {
            var manager = _level();
            var loading = manager == null || manager.IsRoomLoading();
            if (!loading && !manager.InCombatRoom())
            {
                EndRun("outside-run");
                return;
            }
            if (_player()?.IsDead == true)
            {
                EndRun("dead");
                return;
            }

            if (!loading && _ready())
            {
                _owner = _player();
                _transitioning = false;
                FindPauseContext();
            }
            var paused = PauseReason(manager, loading) != "running";
            var expired = _state.Advance(Time.unscaledDeltaTime, paused || _wasPaused);
            _wasPaused = paused;
            if (expired)
                EndStage("expired");
            else if (loading || !_ready())
                _hud.Hide();
            else
                _hud.Update(_state.RemainingSeconds, _state.DurationSeconds);
        }
        catch (Exception exception)
        {
            EndStage("context-error");
            _log.Write($"Context read failed {exception}");
        }
    }

    private string PauseReason(LevelManager? manager, bool loading)
    {
        if (loading)
            return "loading";
        if (!_ready())
            return "player-loading";
        if (_state.RoomCleared && _state.PauseBetweenRooms)
            return "room-cleared";
        if (_pauseScreen?.isPauseScreenDisplayed?.Value == true || Time.timeScale <= 0)
            return "menu";

        var rewardChoiceOpen =
            _state.PauseBetweenRooms
            && _rewardMoment != null
            && _rewardMoment.isActiveAndEnabled
            && _rewardMoment.rewardSpawner?.levelManager?.Pointer == manager!.Pointer
            && _rewardMoment.currentState == RewardMomentController.State.ReadyForCrush;

        return rewardChoiceOpen ? "reward" : "running";
    }

    public void RoomCleared() => _state.ClearRoom();

    public void RoomLoad(bool combat)
    {
        if (!combat)
            EndRun("outside-run-load");

        _state.LoadRoom(combat);
        _owner = null;
        _rewardMoment = null;
        _pauseScreen = null;
        _nextContextSearchAt = 0;
        _transitioning = combat;
        _wasPaused = true;
        _hud.Hide();

        if (_state.Active)
            _log.Write($"Room transition remaining={_state.RemainingSeconds:F2}");
    }

    public void PlayerDestroyed(PlayerHealth player)
    {
        if (_owner?.Pointer != player.Pointer)
            return;
        if (_transitioning || _level()?.IsRoomLoading() == true)
            _owner = null;
        else
            EndRun("player-destroyed");
    }

    public void EndRun(string reason)
    {
        EndStage(reason);
        _state.EndRun();
    }

    public void EndStage(string reason)
    {
        var wasActive = _state.Active || _owner != null;
        _state.EndStage();
        OnEnded();
        _owner = null;
        _hud.Hide();
        if (reason == "shutdown")
            _hud.Dispose();
        if (wasActive)
        {
            _log.Write($"Ended reason={reason}");
        }
    }

    private void FindPauseContext()
    {
        var now = Environment.TickCount64;
        if (now < _nextContextSearchAt)
            return;

        _nextContextSearchAt = now + 500;
        SceneCache.Find(ref _pauseScreen, pause => pause.gameObject.scene.IsValid());
        if (_rewardMoment == null || !_rewardMoment.isActiveAndEnabled)
            _rewardMoment = Resources
                .FindObjectsOfTypeAll<RewardMomentController>()
                .FirstOrDefault(moment =>
                    moment != null
                    && moment.isActiveAndEnabled
                    && moment.gameObject.scene.IsValid()
                    && moment.rewardSpawner?.levelManager?.Pointer == _level()?.Pointer
                );
    }
}
