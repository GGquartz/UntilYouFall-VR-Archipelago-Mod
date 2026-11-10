using HarmonyLib;
using Il2CppSG.Claymore.Entities;
using Il2CppSG.Claymore.HitSystem;
using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.Status;
using Il2CppSG.GlobalEvents.Variables;
using UnityEngine;
using Object = UnityEngine.Object;
using Stasis = Il2CppSG.Claymore.Status.StasisHitProperty.StasisStatusEffect;

namespace UntilYouFallArchipelago;

internal sealed class EnemyStasis : TimedTrap
{
    private sealed record Target(EnemyHealth Health, Stasis Effect)
    {
        public float NativeEndTime { get; set; }
        public int ProtectedHits { get; set; }
    }

    private static EnemyStasis _instance = null!;
    private readonly EnemyStasisState _stasis;
    private readonly Dictionary<IntPtr, Target> _targets = [];
    private StasisHitProperty? _property;
    private long _nextScanAt;
    private long _nextSnapshotAt;

    public EnemyStasis(
        HarmonyLib.Harmony harmony,
        Func<PlayerHealth?> player,
        Func<LevelManager?> level,
        Func<bool> ready,
        Func<int> row,
        Action<string> log
    )
        : base(
            new EnemyStasisState(),
            ApItems.EnemyStasisName,
            "APEnemyStasis",
            row,
            player,
            ready,
            level,
            () => EnemyStasisState.EffectSeconds,
            log,
            ApColors.ModAccentColor
        )
    {
        _instance = this;
        _stasis = (EnemyStasisState)_state;
        harmony.PatchAll(typeof(EnemyStasis));
    }

    public new void Receive()
    {
        if (_stasis.RoomCleared)
        {
            if (!_stasis.InRun || !_ready())
                return;

            EndStage("queue-next-combat");
            _stasis.Receive(true);

            _log.Write("Queued once for next combat");
            return;
        }
        base.Receive();
    }

    protected override void OnActivated()
    {
        _nextScanAt = 0;
        _nextSnapshotAt = 0;
        ApplyToEnemies();
    }

    public new void Update()
    {
        try
        {
            if (_stasis.Pending && _player()?.IsDead == true)
                EndRun("dead");
            if (_stasis.Pending && !_stasis.RoomCleared && _ready())
                base.Receive();
            base.Update();
            if (_stasis.Pending)
            {
                if (_ready())
                    _hud.Update(EnemyStasisState.EffectSeconds, EnemyStasisState.EffectSeconds);
                else
                    _hud.Hide();
            }
            if (!Active || _stasis.RoomCleared || !_ready())
                return;

            if (Environment.TickCount64 >= _nextScanAt)
            {
                _nextScanAt = Environment.TickCount64 + 250;
                ApplyToEnemies();
            }
            if (Environment.TickCount64 < _nextSnapshotAt)
                return;

            _nextSnapshotAt = Environment.TickCount64 + 1000;
            foreach (var target in _targets.Values.ToArray())
            {
                if (!Contains(target))
                    _targets.Remove(target.Effect.Pointer);
            }
        }
        catch (Exception exception)
        {
            EndRun("context-error");
            _log.Write($"Update failed {exception}");
        }
    }

    public new void RoomLoad(bool combat)
    {
        ReleaseTargets();
        base.RoomLoad(combat);
    }

    public new void RoomCleared()
    {
        base.RoomCleared();
        ReleaseTargets();
    }

    protected override void OnEnded() => ReleaseTargets();

    private void ApplyToEnemies()
    {
        var player = _player();
        var manager = _level();
        if (!Active || _stasis.RoomCleared || player == null || !manager.InCombatRoom())
            return;

        if (_property == null)
        {
            var template = Resources
                .FindObjectsOfTypeAll<StasisHitProperty>()
                .FirstOrDefault(candidate => candidate.name == "StasisTrap1");
            if (template == null || template.stasisVariable == null)
                throw new InvalidOperationException("StasisTrap1 asset not loaded");

            _property = Object.Instantiate(template);
            Object.DontDestroyOnLoad(_property);
            _property.name = "AP Enemy Stasis";
            _property.duration = 10f;
            _property.onsetDelay = 0;
            _property.applyThroughBlock = true;
            _property.staminaDamageOnBreak = 0;
            _log.Write(
                $"Property cloned source={template.Pointer} copy={_property.Pointer} breakOnDamage={_property.breakOnDamage}"
            );
        }
        var healthComponents = Resources.FindObjectsOfTypeAll<EnemyHealth>().ToArray();
        foreach (
            var health in healthComponents.Where(health =>
                health.isActiveAndEnabled
                && health.isReady
                && health.CurrentHealth > 0
                && health.gameObject.scene.IsValid()
                && health.gameObject.scene.isLoaded
                && IsRegistered(health)
                && health.Enemy?.CanBeTargeted == true
                && !IsInIntro(health)
            )
        )
        {
            var existing = FindStasis(health);
            if (existing != null && _targets.ContainsKey(existing.Pointer))
            {
                Pin(existing);
                continue;
            }
            var nativeEnd =
                existing == null ? 0
                : existing.onsetDidOccur ? existing.endTime
                : Time.time + existing.property.onsetDelay + existing.property.duration;
            _property.duration = (float)_stasis.RemainingSeconds;
            health.ApplyStatus(_property, new HitData());
            var effect = FindStasis(health);
            if (effect == null)
                throw new InvalidOperationException("Native ApplyStatus created no stasis instance");

            var target = new Target(health, effect) { NativeEndTime = nativeEnd };
            _targets[effect.Pointer] = target;
            if (!effect.onsetDidOccur)
                effect.HandleOnset();
            Pin(effect);

            LogState(target, existing == null ? "applied" : "native overlap protected");
        }
    }

    private static bool IsInIntro(EnemyHealth health)
    {
        try
        {
            return health.Enemy?.Variables?.IsInIntroSequence?.Value == true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsRegistered(EnemyHealth health) =>
        health.Enemy?.EnemyManager?.Items?.Contains(health.Enemy) == true;

    private void ReleaseTargets()
    {
        foreach (var target in _targets.Values.ToArray())
        {
            _targets.Remove(target.Effect.Pointer);
            try
            {
                if (!Contains(target))
                    continue;
                target.Effect.endTime = target.NativeEndTime;
                if (target.NativeEndTime > Time.time)
                {
                    LogState(target, "released to remaining vanilla status");
                    continue;
                }

                if (target.Effect.onsetDidOccur)
                    target.Effect.Complete();
                target.Effect.Clear();
                target.Health.StatusEffects.Remove(target.Effect);

                LogState(target, "released through native complete/clear");
            }
            catch (Exception exception)
            {
                _log.Write($"Native cleanup failed status={target.Effect.Pointer} protection released {exception}");
            }
        }
    }

    private static Stasis? FindStasis(EnemyHealth health)
    {
        if (health.StatusEffects == null)
            return null;
        foreach (var effect in health.StatusEffects)
        {
            var stasis = effect.TryCast<Stasis>();
            if (stasis != null)
                return stasis;
        }

        return null;
    }

    private static bool Contains(Target target)
    {
        if (target.Health == null || target.Health.StatusEffects == null)
            return false;
        foreach (var effect in target.Health.StatusEffects)
            if (effect.Pointer == target.Effect.Pointer)
                return true;

        return false;
    }

    private void Pin(Stasis effect)
    {
        if (Active && _targets.TryGetValue(effect.Pointer, out var target))
            effect.endTime = Math.Max(target.NativeEndTime, Time.time + (float)_stasis.RemainingSeconds);
    }

    [HarmonyPatch(typeof(Stasis), nameof(Stasis.Tick))]
    [HarmonyPrefix]
    private static void RepinBeforeTick(Stasis __instance) => _instance.Pin(__instance);

    [HarmonyPatch(typeof(Stasis), nameof(Stasis.HandleOnset))]
    [HarmonyPostfix]
    private static void RepinAfterOnset(Stasis __instance) => _instance.Pin(__instance);

    [HarmonyPatch(typeof(Stasis), nameof(Stasis.HandleHit))]
    [HarmonyPrefix]
    private static bool AllowHit(Stasis __instance)
    {
        try
        {
            if (
                _instance.Active
                && _instance._targets.TryGetValue(__instance.Pointer, out var target)
                && target.Health != null
                && target.Health.isReady
                && target.Health.CurrentHealth > 0
                && __instance.onsetDidOccur
            )
            {
                target.ProtectedHits++;
                if (target.ProtectedHits == 1)
                    _instance.LogState(target, "first protected hit EnemyHealth damage untouched");

                return false;
            }
        }
        catch (Exception exception)
        {
            _instance._targets.Remove(__instance.Pointer);
            _instance._log.Write($"Hit guard failed open {exception}");
        }

        return true;
    }

    [HarmonyPatch(typeof(Stasis), nameof(Stasis.Complete))]
    [HarmonyPostfix]
    private static void OnComplete(Stasis __instance) => ObserveEnd(__instance, "native complete");

    [HarmonyPatch(typeof(Stasis), nameof(Stasis.Clear))]
    [HarmonyPostfix]
    private static void OnClear(Stasis __instance) => ObserveEnd(__instance, "native clear");

    private static void ObserveEnd(Stasis effect, string phase)
    {
        if (_instance._targets.Remove(effect.Pointer, out var target))
            _instance.LogState(target, phase);
    }

    [HarmonyPatch(typeof(Stasis), nameof(Stasis.Extend))]
    [HarmonyPostfix]
    private static void OnExtend(Stasis __instance, Stasis other)
    {
        if (
            other.property.Pointer == _instance._property?.Pointer
            || !_instance._targets.TryGetValue(__instance.Pointer, out var target)
        )
            return;

        target.NativeEndTime = __instance.endTime;
        _instance.Pin(__instance);
        _instance.LogState(target, "vanilla extend preserved");
    }

    [HarmonyPatch(typeof(LevelManager), nameof(LevelManager.LoadEndRun), typeof(bool))]
    [HarmonyPrefix]
    private static void OnEndRun(LevelManager __instance)
    {
        if (_instance._level()?.Pointer == __instance.Pointer)
            _instance.EndRun("end-run");
    }

    private void LogState(Target target, string phase)
    {
        try
        {
            var enemy = target.Health.Enemy;
            var inStasis = enemy
                .Cast<IVariableResolver>()
                .GetVariable(target.Effect.property.stasisVariable.Cast<Variable>())
                .Cast<BoolVariable>()
                .Value;

            _log.Write(
                $"{phase} enemy={enemy.Pointer} status={target.Effect.Pointer} "
                    + $"remaining={_stasis.RemainingSeconds:F2} nativeEnd={target.NativeEndTime:F3} "
                    + $"onset={target.Effect.onsetDidOccur} endTime={target.Effect.endTime:F3} hits={target.ProtectedHits} "
                    + $"inStasis={inStasis} mover={enemy.EnemyMover?.InStasis} animator={enemy.NPCAnimator?.speed} "
                    + $"hp={target.Health.CurrentHealth} guard={target.Health.CurrentGuard}"
            );
        }
        catch (Exception exception)
        {
            _log.Write($"Snapshot unavailable phase={phase} {exception.Message}");
        }
    }
}
