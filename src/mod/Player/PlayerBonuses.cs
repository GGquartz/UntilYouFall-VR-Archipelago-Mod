using HarmonyLib;
using Il2CppSG.Claymore.Armaments;
using Il2CppSG.Claymore.Armaments.Traits;
using Il2CppSG.Claymore.Combat.CritPoints;
using Il2CppSG.Claymore.Entities;
using Il2CppSG.Claymore.Interaction.Rewards;
using Il2CppSG.Claymore.Movement.Dash;
using Il2CppSG.Claymore.SaveSystem;
using Il2CppSG.Claymore.UI;
using Il2CppSG.Claymore.UI.Inspection;
using Il2CppSG.GlobalEvents.Variables;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UntilYouFallArchipelago;

internal sealed partial class PlayerBonuses
{
    private const string MaximumHealthRewardName = "max_health_increase";
    private const string SuperTraitGuid = "96d7a601e312b824886e2566cd1ed6f8";
    private const string GuardDamageTraitGuid = "8ef41f1d1fd2e814bbb1b2e0419580ab";
    private const string HealthDamageTraitGuid = "fe38e097cd0af9743b702c6e7d0a98df";
    private const float RowStep = 100f;
    private const float HeaderHeight = 60f;
    private static PlayerBonuses _instance = null!;
    private readonly LogChannel _log;
    private readonly LogChannel _damageLog;
    private readonly LogChannel _timingLog;
    private readonly LogChannel _stageProgressLog;
    private readonly Dictionary<long, int> _counts = [];
    private readonly Dictionary<(long ItemId, IntPtr Variable, IntPtr Mod), int> _appliedMods = [];
    private readonly List<Armament> _liveArmaments = [];
    private readonly HashSet<string> _loggedDiagnostics = [];
    private readonly HashSet<IntPtr> _traitsAppliedArmaments = [];
    private readonly Dictionary<IntPtr, int> _enhancedAbilityLevels = [];
    private readonly Dictionary<IntPtr, InspectionLayout> _inspectionLayouts = [];
    private StatModArmamentTrait? _dashChargeTrait;
    private UpgradeSuperTrait? _superTrait;
    private bool _playerStatsReady;
    private PlayerHealth? _player;
    private StatModArmamentTrait? _guardDamageTrait;
    private StatModArmamentTrait? _healthDamageTrait;
    private FloatVariable? _dashRechargeVariable;
    private StatMod? _dashRechargeMod;
    private float _dashRechargeBaseTime;
    private int _dashRechargeApplied;

    public PlayerBonuses(HarmonyLib.Harmony harmony, Action<string> log)
    {
        _instance = this;
        _log = log.Channel("APBonuses");
        _damageLog = log.Channel("APDamage");
        _timingLog = log.Channel("APTiming");
        _stageProgressLog = log.Channel("APStageProgress");
        harmony.PatchAll(typeof(PlayerBonuses));
    }

    public void Update(ArmamentInventory? inventory, IReadOnlyDictionary<long, int>? counts)
    {
        counts ??= new Dictionary<long, int>();
        var changed = PlayerBonusRules.ReceiptItemIds.Any(id =>
            _counts.GetValueOrDefault(id) != PlayerBonusRules.Clamp(id, counts.GetValueOrDefault(id))
        );
        if (changed)
        {
            _counts.Clear();
            foreach (var id in PlayerBonusRules.ReceiptItemIds)
            {
                var count = PlayerBonusRules.Clamp(id, counts.GetValueOrDefault(id));
                if (count > 0)
                    _counts[id] = count;
            }
            _log.Write($"Refreshed {string.Join(", ", _counts.Select(entry => $"{entry.Key}={entry.Value}"))}");
            ApplyDashRecharge(_counts.GetValueOrDefault(ApItems.DashRechargeBonusId));
        }
        var armaments = _liveArmaments.Where(armament => armament != null && armament.BoundHand != null).ToArray();
        if (_counts.Keys.Any(WeaponDamageBonusRules.IsItem))
        {
            var state =
                $"playerReady={_playerStatsReady} tracked={_liveArmaments.Count} bound={armaments.Length} "
                + string.Join(
                    " ",
                    armaments.Select(armament =>
                        $"weapon={armament.ID?.GUID} pointer={armament.Pointer} ready={armament.IsReady} traitsApplied={_traitsAppliedArmaments.Contains(armament.Pointer)}"
                    )
                );

            if (_loggedDiagnostics.Add("damage-gates:" + state))
                _damageLog.Write("Gates " + state);
        }
        foreach (var armament in armaments)
            ApplyBonuses(armament);
        if (changed)
            foreach (var panel in Resources.FindObjectsOfTypeAll<InspectionUIPanel>())
                if (panel.IsActive)
                    panel.RefreshText();
    }

    [HarmonyPatch(typeof(Armament), nameof(Armament.ApplyTraits))]
    [HarmonyPostfix]
    private static void OnTraitsApplied(Armament __instance)
    {
        _instance._traitsAppliedArmaments.Add(__instance.Pointer);
        _instance.Track(__instance);
        if (__instance.boundHand != null && PlayerBonusRules.CanApplyBonuses(_instance._playerStatsReady))
            _instance.ApplyBonuses(__instance);
    }

    [HarmonyPatch(typeof(Armament), nameof(Armament.CollectTraits))]
    [HarmonyPostfix]
    private static void OnTraitsCollected(Armament __instance)
    {
        var added = _instance._traitsAppliedArmaments.Add(__instance.Pointer);
        _instance.Track(__instance);
        if (added)
            _instance._damageLog.Write(
                $"CollectTraits weapon={__instance.ID?.GUID} pointer={__instance.Pointer} bound={__instance.boundHand != null}"
            );
        if (__instance.boundHand != null && PlayerBonusRules.CanApplyBonuses(_instance._playerStatsReady))
            _instance.ApplyBonuses(__instance);
    }

    [HarmonyPatch(typeof(Armament), nameof(Armament.BindToHand))]
    [HarmonyPostfix]
    private static void OnBound(Armament __instance)
    {
        _instance.Track(__instance);
        if (
            _instance._traitsAppliedArmaments.Contains(__instance.Pointer)
            && PlayerBonusRules.CanApplyBonuses(_instance._playerStatsReady)
        )
            _instance.ApplyBonuses(__instance);
    }

    [HarmonyPatch(typeof(Armament), nameof(Armament.OnDestroy))]
    [HarmonyPrefix]
    private static void OnArmamentDestroyed(Armament __instance)
    {
        _instance._liveArmaments.RemoveAll(armament => armament.Pointer == __instance.Pointer);
        _instance._traitsAppliedArmaments.Remove(__instance.Pointer);
        _instance._enhancedAbilityLevels.Remove(__instance.Pointer);
    }

    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Start))]
    [HarmonyPrefix]
    private static void OnPlayerStarting(PlayerHealth __instance)
    {
        _instance._playerStatsReady = true;
        _instance._player = __instance;
        _instance._appliedMods.Clear();
        _instance._enhancedAbilityLevels.Clear();
        var before = __instance.MaxHealth;
        _instance.ApplyMaximumHealth(__instance);
        if (_instance._counts.GetValueOrDefault(ApItems.MaximumHealthBonusId) > 0)
            _instance._log.Write(
                $"Player start replay MaxHealth={before:F1}->{__instance.MaxHealth:F1} savedHP={__instance.savedPlayerHealth.Value:F1}"
            );
        foreach (
            var armament in _instance._liveArmaments.Where(armament =>
                armament != null
                && armament.BoundHand != null
                && _instance._traitsAppliedArmaments.Contains(armament.Pointer)
            )
        )
            _instance.ApplyBonuses(armament);
    }

    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.OnDestroy))]
    [HarmonyPrefix]
    private static void OnPlayerDestroyed()
    {
        _instance._playerStatsReady = false;
        _instance._player = null;
        _instance._appliedMods.Clear();
        _instance._enhancedAbilityLevels.Clear();
        _instance._traitsAppliedArmaments.Clear();
    }

    private void Track(Armament armament)
    {
        if (_liveArmaments.All(existing => existing.Pointer != armament.Pointer))
            _liveArmaments.Add(armament);
    }

    private void ApplyBonuses(Armament armament)
    {
        if (!PlayerBonusRules.CanApplyBonuses(_playerStatsReady))
            return;

        ApplyEnhancedSuper(armament);
        ApplyPlayerStats(armament);
        ApplyWeaponDamage(armament, WeaponDamageKind.Health);
        ApplyWeaponDamage(armament, WeaponDamageKind.Guard);
    }

    private void ApplyWeaponDamage(Armament armament, WeaponDamageKind kind)
    {
        var weapon = WeaponCatalog.ByGuid(armament.ID?.GUID ?? "");
        if (
            weapon == null
            || weapon.NoDamageItems
            || !armament.IsReady
            || !_traitsAppliedArmaments.Contains(armament.Pointer)
        )
            return;
        var itemId = WeaponDamageBonusRules.ItemId(weapon.Id, kind);
        var wanted = _counts.GetValueOrDefault(itemId);
        if (wanted == 0)
            return;

        var guard = kind == WeaponDamageKind.Guard;
        var traitGuid = guard ? GuardDamageTraitGuid : HealthDamageTraitGuid;
        var trait = SceneCache.Find(
            ref (guard ? ref _guardDamageTrait : ref _healthDamageTrait),
            candidate => candidate.GUID == traitGuid
        );
        if (trait == null || trait.StatMods.Length != 1)
        {
            if (_loggedDiagnostics.Add($"damage-trait:{guard}"))
                _damageLog.Write($"Native damage trait unavailable guard={guard} will retry");
            return;
        }
        var mod = trait.StatMods[0];
        var variable = armament.Stats.GetVariable(mod.Variable);
        if (variable == null || mod.Amount <= 0)
        {
            if (_loggedDiagnostics.Add($"damage-resolve:{armament.Pointer}:{itemId}:{mod.Pointer}"))
                _damageLog.Write(
                    $"Resolve skipped weapon={weapon.Name} guard={guard} requested={mod.Variable?.name} resolved={variable != null} amount={mod.Amount}"
                );
            return;
        }
        var key = (itemId, variable.Pointer, mod.Pointer);
        var current = _appliedMods.GetValueOrDefault(key);
        var delta = PlayerBonusRules.RemainingApplications(wanted, current);
        if (delta == 0)
            return;

        var before = DamageValue(armament, kind);
        ArmamentStats.ApplyMod(variable, mod, delta / mod.Amount);
        _appliedMods[key] = wanted;
        var after = DamageValue(armament, kind);

        _damageLog.Write(
            $"Apply weapon={weapon.Name} guid={weapon.Guid} guard={guard} variable={variable.Pointer} copies={current}->{wanted} base={before}->{after} expected={before + delta} match={Math.Abs(after - before - delta) < 0.001f}"
        );
    }

    private static float DamageValue(Armament armament, WeaponDamageKind kind) =>
        kind == WeaponDamageKind.Guard
            ? armament.InstancedVariables.GuardDamage.Value
            : armament.InstancedVariables.HealthDamage.Value;

    private void ApplyPlayerStats(Armament armament)
    {
        ApplyRewardTrait(armament, ApItems.MaximumHealthBonusId, MaximumHealthRewardName);
        ApplyRewardTrait(armament, ApItems.DashImpactBonusId, "dash_damage_increase");
        ApplyRewardTrait(armament, ApItems.ComboLimitBonusId, "combo_increase");
        ApplyDashCharges(armament);
    }

    [HarmonyPatch(typeof(CritPointInstanced), nameof(CritPointInstanced.SpawnCritPoint))]
    [HarmonyPrefix]
    private static void OnCriticalWindowStarting(ref float __3)
    {
        var count = _instance._counts.GetValueOrDefault(ApItems.CriticalWindowBonusId);
        if (count == 0 || __3 <= 0f)
            return;

        __3 *= PlayerBonusRules.CriticalWindowMultiplier(count);
    }

    [HarmonyPatch(typeof(PlayerDash), nameof(PlayerDash.Start))]
    [HarmonyPostfix]
    private static void OnDashStarting(PlayerDash __instance)
    {
        var variable = __instance._dashChargeRestoreTimeVariable;
        if (variable == null)
        {
            _instance._timingLog.Write("Dash interval unavailable at PlayerDash.Start");
            return;
        }
        if (_instance._dashRechargeVariable?.Pointer != variable.Pointer)
        {
            _instance.ApplyDashRecharge(0);
            _instance._dashRechargeVariable = variable;
            _instance._dashRechargeBaseTime = variable.Value;
            _instance._dashRechargeMod = new StatMod { Variable = variable };
            _instance._dashRechargeApplied = 0;
        }
        _instance.ApplyDashRecharge(_instance._counts.GetValueOrDefault(ApItems.DashRechargeBonusId));
    }

    private void ApplyDashRecharge(int wanted)
    {
        if (_dashRechargeVariable == null || _dashRechargeMod == null || wanted == _dashRechargeApplied)
            return;

        _dashRechargeMod.Amount = PlayerBonusRules.DashRechargeDelta(
            _dashRechargeBaseTime,
            wanted,
            _dashRechargeApplied
        );
        ArmamentStats.ApplyMod(_dashRechargeVariable, _dashRechargeMod, 1f);
        _dashRechargeApplied = wanted;
    }

    private void ApplyMaximumHealth(PlayerHealth player)
    {
        var wanted = _counts.GetValueOrDefault(ApItems.MaximumHealthBonusId);
        if (wanted == 0)
            return;

        var statTrait = SceneCache.Reward(MaximumHealthRewardName)?.Trait?.TryCast<StatModArmamentTrait>();
        if (statTrait == null || statTrait.StatMods.Length != 1)
        {
            _log.Write("Native max health stat mod was not found");
            return;
        }
        var variable = player.health.Get(player.Entity.Cast<IVariableResolver>()).Max;
        ApplyResolvedMod(variable, ApItems.MaximumHealthBonusId, statTrait.StatMods[0], wanted);
    }

    private float NativeHealthPipCapacity()
    {
        var cap = (float)UIPipRenderer.PIP_MAX;
        if (_loggedDiagnostics.Add("pip-capacity"))
            _log.Write($"Native pip capacity PIP_MAX={cap:F0}");

        return cap > 0f ? cap : PlayerBonusRules.FallbackMaxHealthCap;
    }

    private void ApplyRewardTrait(Armament armament, long itemId, string developerName)
    {
        var wanted = _counts.GetValueOrDefault(itemId);
        if (wanted == 0)
            return;

        var reward = SceneCache.Reward(developerName);
        if (reward == null)
        {
            _log.Write($"Native reward not found: {developerName}");
            return;
        }
        var trait = reward.Trait;
        var statTrait = trait?.TryCast<StatModArmamentTrait>();
        if (statTrait == null)
            return;

        foreach (var mod in statTrait.StatMods)
            ApplyMod(armament, itemId, developerName, mod, wanted);
    }

    private void ApplyDashCharges(Armament armament)
    {
        var wanted = _counts.GetValueOrDefault(ApItems.DashChargesBonusId);
        if (wanted == 0)
            return;
        var trait = DashChargeTrait();
        if (trait == null)
            return;

        foreach (var mod in trait.StatMods)
            ApplyMod(armament, ApItems.DashChargesBonusId, "dash_charges", mod, wanted);
    }

    private void ApplyMod(Armament armament, long itemId, string source, StatMod mod, int wanted)
    {
        var variable = armament.Stats.GetVariable(mod.Variable);
        if (variable == null)
        {
            var diagnostic = $"unresolved:{source}:{mod.Pointer}";
            if (_loggedDiagnostics.Add(diagnostic))
                _log.Write(
                    $"Variable unresolved reward={source} requested={mod.Variable?.name ?? "null"} weapon={armament.name}"
                );
            return;
        }
        ApplyResolvedMod(variable, itemId, mod, wanted);
    }

    private void ApplyResolvedMod(Variable variable, long itemId, StatMod mod, int wanted)
    {
        var isMaxHealth = itemId == ApItems.MaximumHealthBonusId;
        var key = (itemId, isMaxHealth ? IntPtr.Zero : variable.Pointer, mod.Pointer);
        var current = _appliedMods.GetValueOrDefault(key);
        var remaining = PlayerBonusRules.RemainingApplications(wanted, current);
        if (remaining == 0)
            return;

        var applied = isMaxHealth ? CapMaximumHealthCopies(mod, wanted, remaining) : remaining;
        var healthBefore = isMaxHealth ? _player?.MaxHealth ?? 0f : 0f;
        if (applied > 0)
            ArmamentStats.ApplyMod(variable, mod, applied);
        if (isMaxHealth && applied > 0)
            _log.Write(
                $"Max health applied wanted={wanted} recorded={current} applying={applied} MaxHealth={healthBefore:F1}->{_player?.MaxHealth ?? 0f:F1}"
            );

        var capReached = isMaxHealth && _player != null;
        _appliedMods[key] = applied == remaining || capReached ? wanted : current + applied;
    }

    private int CapMaximumHealthCopies(StatMod mod, int wanted, int remaining)
    {
        if (_player == null)
            return 0;

        var cap = NativeHealthPipCapacity();
        var allowed = PlayerBonusRules.AllowedMaxHealthApplications(cap, _player.MaxHealth, mod.Amount, remaining);
        if (allowed < remaining && _loggedDiagnostics.Add($"max-health-cap:{_player.MaxHealth}:{wanted}"))
            _log.Write(
                $"Max health capped at {cap:F0}: current={_player.MaxHealth:F1} wanted={wanted} applying={allowed} of {remaining}"
            );

        return allowed;
    }

    private StatModArmamentTrait? DashChargeTrait()
    {
        SceneCache.Find(
            ref _dashChargeTrait,
            trait =>
                trait.StatMods.Length == 1
                && trait.StatMods[0].Variable?.name == "MaxDashCharges"
                && trait.StatMods[0].Amount == 1f
        );

        if (_dashChargeTrait == null)
            _log.Write("Native dash charges trait was not found");
        return _dashChargeTrait;
    }

    private void ApplyEnhancedSuper(Armament armament)
    {
        if (_counts.GetValueOrDefault(ApItems.EnhanceSuperBonusId) == 0)
            return;

        var ability = armament.Ability;
        if (ability == null)
        {
            _log.Write($"Enhance super skipped weapon={armament.name} ability=null");
            return;
        }
        var before = ability.AbilityLevel;
        if (!PlayerBonusRules.CanEnhanceSuper(before, ability.MaxAbilityLevel))
            return;
        if (_enhancedAbilityLevels.TryGetValue(armament.Pointer, out var enhancedLevel) && before >= enhancedLevel)
            return;
        var trait = SuperTrait();
        if (trait == null)
            return;

        trait.Apply(armament);
        var after = armament.Ability?.AbilityLevel ?? before;
        _enhancedAbilityLevels[armament.Pointer] = after;
    }

    private UpgradeSuperTrait? SuperTrait()
    {
        SceneCache.Find(ref _superTrait, trait => trait.GUID == SuperTraitGuid);
        if (_superTrait == null)
            _log.Write("Native enhance super trait was not found");
        return _superTrait;
    }
}
