using Il2CppSG.Claymore.Entities;
using Il2CppSG.Claymore.Level;

namespace UntilYouFallArchipelago;

internal sealed class PlayerVitality
{
    private readonly Func<PlayerHealth?> _player;
    private readonly Func<LevelManager?> _levelManager;
    private readonly LogChannel _log;

    public PlayerVitality(Func<PlayerHealth?> player, Func<LevelManager?> levelManager, Action<string> log)
    {
        _player = player;
        _levelManager = levelManager;
        _log = log.Channel("APVitality");
    }

    public bool CanHeal()
    {
        var player = _player();
        var manager = _levelManager();
        return player != null
            && player.isActiveAndEnabled
            && !player.IsDead
            && manager != null
            && !manager.IsRoomLoading()
            && manager.HasEncounter();
    }

    public void Restore(int amount)
    {
        var player = _player()!;
        var before = player.CurrentHealth;
        player.Restore(amount);
        _log.Write($"Applied amount={amount} HP={before:F1}->{player.CurrentHealth:F1}");
    }
}
