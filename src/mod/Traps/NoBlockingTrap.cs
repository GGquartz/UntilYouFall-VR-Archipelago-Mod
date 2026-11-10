using HarmonyLib;
using Il2CppSG.Claymore.Combat.Blocking;
using Il2CppSG.Claymore.Entities;
using Il2CppSG.Claymore.Interaction.Rewards;
using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.NPCs;
using Il2CppSG.Claymore.UI;
using UnityEngine;

namespace UntilYouFallArchipelago;

internal sealed class NoBlockingTrap : TimedTrap
{
    private static NoBlockingTrap _instance = null!;
    private readonly NoBlockingTrapState _dodgeState;

    public NoBlockingTrap(
        HarmonyLib.Harmony harmony,
        Func<PlayerHealth?> player,
        Func<bool> ready,
        Func<LevelManager?> level,
        Func<int> durationSeconds,
        Action<string> log
    )
        : this(harmony, new NoBlockingTrapState(), player, ready, level, durationSeconds, log) { }

    private NoBlockingTrap(
        HarmonyLib.Harmony harmony,
        NoBlockingTrapState state,
        Func<PlayerHealth?> player,
        Func<bool> ready,
        Func<LevelManager?> level,
        Func<int> durationSeconds,
        Action<string> log
    )
        : base(
            state,
            "Dodge Only",
            "APNoBlockTrap",
            () => TimedTrapHudLayout.DodgeRow,
            player,
            ready,
            level,
            durationSeconds,
            log
        )
    {
        _instance = this;
        _dodgeState = state;
        harmony.PatchAll(typeof(NoBlockingTrap));
    }

    protected override bool Activate(int durationSeconds)
    {
        if (_state.Active)
            return _dodgeState.TryActivate(true, Array.Empty<(IntPtr, float)>(), durationSeconds);

        var projectiles = Resources
            .FindObjectsOfTypeAll<EnemyProjectile>()
            .Where(projectile => projectile != null && projectile.isActiveAndEnabled && projectile.IsAlive)
            .Select(projectile => (projectile.Pointer, projectile._startTime))
            .ToArray();

        return _dodgeState.TryActivate(true, projectiles, durationSeconds);
    }

    private bool CanApply()
    {
        if (_state.Active && _owner == null && _ready())
            _owner = _player();
        return _dodgeState.ShouldForceDodge(_owner != null && _player()?.Pointer == _owner.Pointer, _ready());
    }

    [HarmonyPatch(typeof(PlayerDefense), nameof(PlayerDefense.HandleAttack))]
    [HarmonyPrefix]
    private static void BeginAttack(PlayerDefense __instance, out bool? __state)
    {
        __state = null;
        if (!_instance._state.Active)
            return;
        try
        {
            if (!_instance.CanApply())
            {
                return;
            }
            if (__instance.health?.Pointer != _instance._owner?.Pointer)
            {
                return;
            }
            __state = __instance.ForceDodgeVisuals;
            __instance.ForceDodgeVisuals = true;
        }
        catch (Exception exception)
        {
            _instance.EndStage("attack-prefix-error");
            _instance._log.Write($"Attack override failed {exception}");
        }
    }

    [HarmonyPatch(typeof(PlayerDefense), nameof(PlayerDefense.HandleAttack))]
    [HarmonyFinalizer]
    private static Exception? EndAttack(PlayerDefense __instance, bool? __state, Exception? __exception)
    {
        if (!__state.HasValue)
            return __exception;
        try
        {
            __instance.ForceDodgeVisuals = __state.Value;
            if (__exception != null)
                _instance.EndStage("native-attack-error");
        }
        catch (Exception exception)
        {
            _instance.EndStage("attack-finalizer-error");
            _instance._log.Write($"Attack restore/read failed {exception}");
        }
        return __exception;
    }

    [HarmonyPatch(typeof(EnemyProjectile), nameof(EnemyProjectile.DoBlockCheck))]
    [HarmonyPrefix]
    private static bool AllowProjectileBlock(EnemyProjectile __instance)
    {
        if (!_instance._state.Active)
            return true;
        try
        {
            if (!_instance.CanApply())
            {
                return true;
            }
            var projectile = __instance;
            var key = (Pointer: projectile.Pointer, StartedAt: projectile._startTime);
            if (!_instance._dodgeState.SuppressProjectileBlock(key.Pointer, key.StartedAt))
                return true;
            return false;
        }
        catch (Exception exception)
        {
            _instance.EndStage("projectile-error");
            _instance._log.Write($"Projectile guard failed {exception}");
            return true;
        }
    }

    [HarmonyPatch(typeof(LevelManager), nameof(LevelManager.LoadEndRun), typeof(bool))]
    [HarmonyPrefix]
    private static void OnEndRun(LevelManager __instance)
    {
        if (_instance._level()?.Pointer == __instance.Pointer)
            _instance.EndRun("end-run");
    }
}
