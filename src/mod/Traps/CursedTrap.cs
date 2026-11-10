using HarmonyLib;
using Il2CppSG.Claymore.Entities;
using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.NPCs;

namespace UntilYouFallArchipelago;

internal sealed class CursedTrap : TimedTrap
{
    private static CursedTrap _instance = null!;
    private readonly CursedTrapState _curseState;

    public CursedTrap(
        HarmonyLib.Harmony harmony,
        Func<PlayerHealth?> player,
        Func<bool> ready,
        Func<LevelManager?> level,
        Func<int> row,
        Func<int> durationSeconds,
        Action<string> log
    )
        : this(harmony, new CursedTrapState(), player, ready, level, row, durationSeconds, log) { }

    private CursedTrap(
        HarmonyLib.Harmony harmony,
        CursedTrapState state,
        Func<PlayerHealth?> player,
        Func<bool> ready,
        Func<LevelManager?> level,
        Func<int> row,
        Func<int> durationSeconds,
        Action<string> log
    )
        : base(state, "Cursed", "APCursedTrap", row, player, ready, level, durationSeconds, log)
    {
        _instance = this;
        _curseState = state;
        harmony.Patch(
            AccessTools.Method(typeof(PlayerHealth), nameof(PlayerHealth.Damage)),
            prefix: new HarmonyMethod(typeof(CursedTrap), nameof(BeforeDamage)),
            postfix: new HarmonyMethod(typeof(CursedTrap), nameof(AfterDamage))
        );
        harmony.Patch(
            AccessTools.Method(typeof(LevelManager), nameof(LevelManager.LoadEndRun), [typeof(bool)]),
            prefix: new HarmonyMethod(typeof(CursedTrap), nameof(OnEndRun))
        );
    }

    private bool Applies(PlayerHealth health)
    {
        var manager = _level();

        return Active
            && _state.InRun
            && _ready()
            && manager.InCombatRoom()
            && health != null
            && health.isActiveAndEnabled
            && !health.IsDead
            && health.gameObject.scene.IsValid()
            && health.Pointer == _player()?.Pointer;
    }

    private static void BeforeDamage(PlayerHealth __instance, Enemy __0, ref float __1, out float? __state)
    {
        __state = null;
        var original = __1;
        try
        {
            if (__0 == null || __1 <= 0f || !_instance.Applies(__instance))
                return;

            var scaled = _instance._curseState.ScaleDamage(__1);
            var before = __instance.CurrentHealth;
            _instance._log.Write(
                $"Damage input player={__instance.Pointer} attacker={__0.Pointer} amount={original:F3}->{scaled:F3} scalar={__instance.damageScalar?.Value} hp={before:F3} invulnerable={__instance.IsInvulnerable} deathsDoor={__instance.IsOnDeathsDoor} remaining={_instance._state.RemainingSeconds:F2}"
            );
            __1 = scaled;
            __state = before;
        }
        catch (Exception exception)
        {
            __1 = original;
            __state = null;
            _instance.EndStage("damage-prefix-error");
            _instance._log.Write($"Damage override failed {exception}");
        }
    }

    private static void AfterDamage(PlayerHealth __instance, float? __state, float __result)
    {
        if (!__state.HasValue)
            return;

        try
        {
            _instance._log.Write(
                $"Damage result player={__instance.Pointer} hp={__state.Value:F3}->{__instance.CurrentHealth:F3} nativeResult={__result:F3} invulnerable={__instance.IsInvulnerable} deathsDoor={__instance.IsOnDeathsDoor} dead={__instance.IsDead}"
            );
        }
        catch (Exception exception)
        {
            _instance._log.Write($"Cannot read damage result {exception}");
        }
    }

    private static void OnEndRun(LevelManager __instance)
    {
        if (_instance._level()?.Pointer == __instance.Pointer)
            _instance.EndRun("end-run");
    }
}
