using HarmonyLib;
using Il2CppSG.Claymore.Combat.Combatants;
using Il2CppSG.Claymore.HitSystem;
using Il2CppSG.Claymore.Level;

namespace UntilYouFallArchipelago;

internal sealed class EnemyKillChecks
{
    private static EnemyKillChecks _instance = null!;
    private readonly Func<LevelManager?> _level;
    private readonly Func<bool> _playerAlive;
    private readonly Action<long> _record;
    private readonly Action<EnemyCheck> _onKill;
    private readonly LogChannel _log;
    private readonly HashSet<string> _unknownGuids = [];

    public EnemyKillChecks(
        HarmonyLib.Harmony harmony,
        Func<LevelManager?> level,
        Func<bool> playerAlive,
        Action<long> record,
        Action<EnemyCheck> onKill,
        Action<string> log
    )
    {
        _instance = this;
        _level = level;
        _playerAlive = playerAlive;
        _record = record;
        _onKill = onKill;
        _log = log.Channel("APEnemy");
        harmony.PatchAll(typeof(EnemyKillChecks));

        _log.Write($"Ready types={EnemyChecks.All.Length} source=EnemyKillController.Kill");
    }

    [HarmonyPatch(typeof(EnemyKillController), nameof(EnemyKillController.Kill))]
    [HarmonyPrefix]
    private static void BeforeKill(EnemyKillController __instance, out EnemyCheck? __state)
    {
        __state = null;
        try
        {
            var manager = _instance._level();
            var enemy = __instance.Enemy;
            var health = enemy?.EnemyHealth;
            if (manager?.Campaign?.GUID == StageChecks.CampaignGuid && manager.LevelIndex == StageChecks.Count - 1)
            {
                var definition = enemy?.EntityDefinition?.TryCast<NPCCombatantDefinition>();
                _instance._log.Write(
                    $"Final stage death prefix guid={definition?.GUID} definition={definition?.DeveloperName} health={health?.CurrentHealth} dying={__instance._isDying} alive={_instance._playerAlive()} loading={manager.IsRoomLoading()}"
                );
            }
            if (
                manager == null
                || health == null
                || !EnemyChecks.IsCombatKill(
                    manager.Campaign?.GUID,
                    manager.LevelIndex,
                    manager.IsRoomLoading(),
                    _instance._playerAlive(),
                    __instance._isDying,
                    health.CurrentHealth
                )
            )
                return;

            var combatant = enemy!.EntityDefinition?.TryCast<NPCCombatantDefinition>();
            if (combatant == null)
                return;

            __state = EnemyChecks.ByGuid(combatant.GUID);
            if (__state == null && _instance._unknownGuids.Add(combatant.GUID))
                _instance._log.Write(
                    $"Unmapped combat death guid={combatant.GUID} definition={combatant.DeveloperName}"
                );
        }
        catch (Exception ex)
        {
            _instance._log.Write("Failed to identify combat death: " + ex.Message);
        }
    }

    [HarmonyPatch(typeof(EnemyKillController), nameof(EnemyKillController.Kill))]
    [HarmonyPostfix]
    private static void AfterKill(EnemyKillController __instance, EnemyCheck? __state)
    {
        try
        {
            var manager = _instance._level();
            if (manager?.Campaign?.GUID == StageChecks.CampaignGuid && manager.LevelIndex == StageChecks.Count - 1)
                _instance._log.Write(
                    $"Final stage death postfix mapped={__state?.Name ?? "none"} dying={__instance._isDying}"
                );
            if (__state == null)
                return;
            if (!__instance._isDying)
                return;

            _instance._record(__state.Id);
            _instance._onKill(__state);
        }
        catch (Exception ex)
        {
            _instance._log.Write("Failed to record kill: " + ex.Message);
        }
    }
}
