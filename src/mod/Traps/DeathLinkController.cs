using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppSG.Claymore.Combat.Combatants;
using Il2CppSG.Claymore.Entities;
using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.NPCs;
using Il2CppSG.Claymore.Player;
using Il2CppSG.Claymore.UI;

namespace UntilYouFallArchipelago;

internal sealed class DeathLinkController
{
    private static readonly TimeSpan PendingTtl = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SelfKillWindow = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan VoluntaryExitWindow = TimeSpan.FromSeconds(8);
    private static DeathLinkController _instance = null!;
    private readonly ArchipelagoClient _client;
    private readonly Func<LevelManager?> _level;
    private readonly Func<PlayerHealth?> _health;
    private readonly Func<bool> _canApply;
    private readonly Action<ApNotice> _notify;
    private readonly (string Name, Action Apply)[] _traps;
    private readonly LogChannel _log;
    private readonly Random _random = new();
    private (string Source, string Cause, DateTime At)? _pending;
    private DateTime _selfKillAt = DateTime.MinValue;
    private DateTime _lastSentAt = DateTime.MinValue;
    private DateTime _voluntaryExitAt = DateTime.MinValue;

    public DeathLinkController(
        HarmonyLib.Harmony harmony,
        ArchipelagoClient client,
        Func<LevelManager?> level,
        Func<PlayerHealth?> health,
        Func<bool> canApply,
        Action<ApNotice> notify,
        (string Name, Action Apply)[] traps,
        Action<string> log
    )
    {
        _instance = this;
        _client = client;
        _level = level;
        _health = health;
        _canApply = canApply;
        _notify = notify;
        _traps = traps;
        _log = log.Channel("APDeathLink");
        client.DeathLinkReceived = Received;

        var target = AccessTools.Method(typeof(PlayerDeathController), nameof(PlayerDeathController.OnPlayerKilled));
        if (target == null)
        {
            _log.Write("OnPlayerKilled not found");
            return;
        }
        foreach (
            var name in new[] { nameof(PauseScreen.HandleGiveUpButton), nameof(PauseScreen.HandleQuitToTitleButton) }
        )
        {
            var exit = AccessTools.Method(typeof(PauseScreen), name);
            if (exit == null)
            {
                _log.Write($"{name} not found");
                return;
            }
            harmony.Hook<DeathLinkController>(exit, prefix: nameof(BeginVoluntaryExit));
        }

        harmony.Hook<DeathLinkController>(target, postfix: nameof(Killed));
        _log.Write("Ready source=PlayerDeathController.OnPlayerKilled");
    }

    private static void BeginVoluntaryExit(MethodBase __originalMethod)
    {
        _instance._voluntaryExitAt = DateTime.UtcNow;
        _instance._log.Write($"Voluntary exit begin handler={__originalMethod.Name}");
    }

    private bool InCampaign()
    {
        var manager = _level();
        return manager != null
            && !manager.IsRoomLoading()
            && StageChecks.TryGetStage(manager.Campaign?.GUID, manager.LevelIndex, true, out _);
    }

    private static void Killed(Enemy killer)
    {
        try
        {
            _instance.Send(killer);
        }
        catch (Exception ex)
        {
            _instance._log.Write("Send failed: " + ex.Message);
        }
    }

    private void Send(Enemy? killer)
    {
        var now = DateTime.UtcNow;
        if (_client.DeathLinkMode == DeathLinkMode.Off)
            return;

        if (now - _voluntaryExitAt < VoluntaryExitWindow)
        {
            _log.Write("Run exit, not sent");
            return;
        }
        if (now - _selfKillAt < SelfKillWindow)
        {
            _selfKillAt = DateTime.MinValue;
            _log.Write("Death caused by incoming DeathLink");
            return;
        }
        if (!InCampaign())
        {
            _log.Write("Death outside the campaign");
            return;
        }
        if (now - _lastSentAt < TimeSpan.FromSeconds(3))
            return;

        var definition = killer?.EntityDefinition?.TryCast<NPCCombatantDefinition>();
        var name = definition == null ? null : EnemyChecks.ByGuid(definition.GUID)?.Name ?? definition.DeveloperName;
        var alias = _client.PlayerAlias;
        var cause = string.IsNullOrWhiteSpace(name) ? $"{alias} died" : $"{alias} was slain by {name}";
        _lastSentAt = now;

        _log.Write($"Sending cause={cause} sent={_client.SendDeathLink(cause)}");
    }

    private void Received(string source, string cause)
    {
        if (!InCampaign())
        {
            _log.Write($"Received outside campaign, discarded source={source}");
            return;
        }

        _log.Write($"Received source={source} cause={cause} mode={_client.DeathLinkMode}");
        _pending = (source, cause, DateTime.UtcNow);
    }

    public void Update()
    {
        if (_pending is not { } pending)
            return;
        if (DateTime.UtcNow - pending.At > PendingTtl)
        {
            _pending = null;
            _log.Write("Pending deathlink expired");
            return;
        }
        var player = _health();
        if (player == null || !_canApply() || !InCampaign())
            return;

        _pending = null;
        var mode = _client.DeathLinkMode;
        var trap = mode == DeathLinkMode.Trap ? _traps[_random.Next(_traps.Length)] : default;
        _notify(ApNotice.DeathLink(pending.Source, pending.Cause, trap.Name));

        switch (mode)
        {
            case DeathLinkMode.Hard:
                _selfKillAt = DateTime.UtcNow;
                player.KillPlayer();
                _log.Write("Player killed");
                break;
            case DeathLinkMode.Soft:
                var before = player.CurrentHealth;
                if (before > 1f)
                    player.Set(1f);
                _log.Write(
                    $"Applied soft HP={before:F1}->{player.CurrentHealth:F1} DeathsDoor={player.IsOnDeathsDoor}"
                );
                break;
            case DeathLinkMode.Trap:
                trap.Apply();
                _log.Write($"Applied trap name={trap.Name}");
                break;
            case DeathLinkMode.DeathsDoor:
                if (player.IsOnDeathsDoor)
                {
                    _log.Write("Deaths door already active");
                    break;
                }
                var healthBefore = player.CurrentHealth;
                _log.Write(
                    $"Entering deaths door HP={healthBefore:F1} dead={player.IsDead} invulnerable={player.IsInvulnerable} lastHit={player.timeLastHit:F3} invulnerabilityAt={player.timeEnteredInvulnerability:F3}"
                );
                player.DealMassiveDamage();
                _log.Write(
                    $"Applied deaths door HP={healthBefore:F1}->{player.CurrentHealth:F1} DeathsDoor={player.IsOnDeathsDoor} dead={player.IsDead} invulnerable={player.IsInvulnerable} lastHit={player.timeLastHit:F3} invulnerabilityAt={player.timeEnteredInvulnerability:F3}"
                );
                break;
        }
    }
}
