using HarmonyLib;
using Il2Cpp;
using Il2CppSG.Claymore.Armaments;
using Il2CppSG.Claymore.Armaments.Traits;
using Il2CppSG.Claymore.Combat.Blocking;
using Il2CppSG.Claymore.Combat.Combatants;
using Il2CppSG.Claymore.Entities;
using Il2CppSG.Claymore.HitSystem;
using Il2CppSG.Claymore.Interaction;
using Il2CppSG.Claymore.Interaction.Rewards;
using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.NPCs;
using Il2CppSG.Claymore.Player;
using Il2CppSG.Claymore.SaveSystem;
using Il2CppSG.Claymore.UI;

namespace UntilYouFallArchipelago;

internal sealed class QuestEventHooks
{
    private const int MasterworkLevel = WeaponUpgradeCapRules.MaximumItemCount;
    private static readonly Dictionary<string, QuestSource> SuperKillQuests = new()
    {
        ["Vicious Strike"] = QuestSource.ViciousKills,
    };
    private const float ProjectileKillWindow = 1f;
    private static readonly Dictionary<IntPtr, (QuestSource Source, float Until)> ProjectileHits = [];
    private static QuestEventHooks _instance = null!;
    private bool _actDamaged;
    private int _cleanStreak;
    private int _deathsDoorEntries;
    private const float CrowdWindow = 5f;
    private readonly Queue<float> _recentKills = new();
    private const float SweepWindow = 0.5f;
    private static readonly int SweepQuest = Array.FindIndex(
        QuestChecks.All,
        quest => quest.Source == QuestSource.MultiHitSwings
    );
    private IntPtr _sweepWeapon;
    private IntPtr _sweepEnemy;
    private float _sweepTime;
    private bool _wasOnDeathsDoor;
    private readonly ArchipelagoClient _client;
    private readonly Func<LevelManager?> _level;
    private readonly LogChannel _log;

    public QuestEventHooks(
        HarmonyLib.Harmony harmony,
        ArchipelagoClient client,
        Func<LevelManager?> level,
        Action<string> log
    )
    {
        _instance = this;
        _client = client;
        _level = level;
        _log = log.Channel("APQuestEvents");
        try
        {
            harmony.PatchAll(typeof(QuestEventHooks));
        }
        catch (Exception exception)
        {
            _log.Write($"Quest tracking disabled: {exception.Message}");
        }
    }

    private bool InRun()
    {
        var manager = _level();

        return _client.ActiveQuests.Length > 0
            && manager != null
            && !manager.IsRoomLoading()
            && StageChecks.TryGetStage(manager.Campaign?.GUID, manager.LevelIndex, true, out _);
    }

    private static bool Tracked(QuestSource source) => _instance._client.QuestTracked(source);

    private static void Count(QuestSource source) =>
        Handle(source, () => _instance._client.AddQuestProgress(source, 1));

    private static void Handle(QuestSource source, Action action) => Handle(source.ToString(), action);

    private static void Handle(string source, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _instance._log.Write($"Failed to track {source}: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(PlayerDefense), nameof(PlayerDefense.OnAttackBlocked))]
    [HarmonyPostfix]
    private static void Blocked()
    {
        var melee = Tracked(QuestSource.MeleeBlocks);
        var bulwark = Tracked(QuestSource.BulwarkBlocks);
        if ((!melee && !bulwark) || !_instance.InRun())
            return;

        if (melee)
            Count(QuestSource.MeleeBlocks);
        if (!bulwark)
            return;

        try
        {
            foreach (var (weapon, active) in HeldSupers())
            {
                if (weapon.Super != "Bulwark")
                    continue;
                if (active)
                {
                    Count(QuestSource.BulwarkBlocks);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _instance._log.Write("Failed to read ability on block: " + ex.Message);
        }
    }

    [HarmonyPatch(typeof(PlayerDefense), nameof(PlayerDefense.OnAttackDodged))]
    [HarmonyPostfix]
    private static void Dodged()
    {
        if (Tracked(QuestSource.Dodges) && _instance.InRun())
            Count(QuestSource.Dodges);
    }

    [HarmonyPatch(typeof(EnemyProjectile), nameof(EnemyProjectile.OnPlayerDestroyProjectile))]
    [HarmonyPostfix]
    private static void Deflected()
    {
        if (Tracked(QuestSource.RangedBlocks) && _instance.InRun())
            Count(QuestSource.RangedBlocks);
    }

    [HarmonyPatch(typeof(EnemyHealth), nameof(EnemyHealth.BreakGuard))]
    [HarmonyPrefix]
    private static void GuardBreak(EnemyHealth __instance)
    {
        if (Tracked(QuestSource.GuardBreaks) && _instance.InRun() && !__instance.IsGuardBroken)
            Count(QuestSource.GuardBreaks);
    }

    [HarmonyPatch(
        typeof(Il2CppSG.Claymore.Armaments.Abilities.ArmamentAbility),
        nameof(Il2CppSG.Claymore.Armaments.Abilities.ArmamentAbility.OnActivationSuccess)
    )]
    [HarmonyPostfix]
    private static void SuperUsed(Il2CppSG.Claymore.Armaments.Abilities.ArmamentAbility __instance)
    {
        if (!Tracked(QuestSource.AllSupers))
            return;

        Handle(
            QuestSource.AllSupers,
            () =>
            {
                if (!_instance.InRun())
                    return;
                var weapon = WeaponCatalog.ByGuid(__instance.Armament?.ID?.GUID ?? "");
                if (weapon == null)
                    return;

                var distinct = _instance._client.QuestSuperUsed(weapon.Super);
                _instance._log.Write($"Super used: {weapon.Super} ({weapon.Name}) distinct={distinct}");
            }
        );
    }

    [HarmonyPatch(typeof(LevelManager), nameof(LevelManager.StartRun))]
    [HarmonyPostfix]
    private static void Started(LevelManager __instance) =>
        Handle(
            "start run",
            () =>
            {
                if (__instance.Campaign?.GUID != StageChecks.CampaignGuid)
                    return;

                _instance._client.QuestRunStarted();
                _instance._actDamaged = false;
                _instance._cleanStreak = 0;
                _instance._deathsDoorEntries = 0;
                _instance._recentKills.Clear();
                _instance._sweepEnemy = IntPtr.Zero;
                _instance._log.Write("New run started");
            }
        );

    [HarmonyPatch(typeof(PlayerCombatant), nameof(PlayerCombatant.OnHealthDamaged))]
    [HarmonyPostfix]
    private static void Combo(int __2, float __3) =>
        Handle(
            QuestSource.LongCombo,
            () =>
            {
                if (Tracked(QuestSource.LongCombo) && _instance.InRun() && __3 > 0 && __2 >= 0)
                    _instance._client.ObserveQuestProgress(QuestSource.LongCombo, __2 + 1);
            }
        );

    [HarmonyPatch(typeof(Armament), nameof(Armament.AddTraitUpgrade))]
    [HarmonyPostfix]
    private static void Traits(Armament __instance) =>
        Handle(
            "traits",
            () =>
            {
                if (
                    !(Tracked(QuestSource.TraitsOnWeapon) || Tracked(QuestSource.TraitsEquipped))
                    || !_instance.InRun()
                    || __instance.BoundHand == null
                )
                    return;

                var count = __instance.TraitsAppliedByRewards + __instance.TraitsAppliedByLevel;
                var other = __instance.BoundHand.OtherHand?.BoundArmament;
                var otherCount =
                    other == null || other.Pointer == __instance.Pointer
                        ? 0
                        : other.TraitsAppliedByRewards + other.TraitsAppliedByLevel;
                _instance._client.ObserveQuestProgress(QuestSource.TraitsOnWeapon, Math.Max(count, otherCount));
                _instance._client.ObserveQuestProgress(QuestSource.TraitsEquipped, count + otherCount);
            }
        );

    [HarmonyPatch(typeof(UpgradeSuperTrait), nameof(UpgradeSuperTrait.OnCrushComplete))]
    [HarmonyPostfix]
    private static void Super(Armament __0) =>
        Handle(
            QuestSource.SuperLevel4,
            () =>
            {
                if (Tracked(QuestSource.SuperLevel4) && _instance.InRun() && __0.Ability != null)
                    _instance._client.ObserveQuestProgress(QuestSource.SuperLevel4, __0.Ability.AbilityLevel + 1);
            }
        );

    [HarmonyPatch(typeof(EnemyKillController), nameof(EnemyKillController.Update))]
    [HarmonyPrefix]
    private static void BeforeEnemyFlightUpdate(EnemyKillController __instance, out bool __state) =>
        __state = __instance._isTrackingLaunchDeath;

    [HarmonyPatch(typeof(EnemyKillController), nameof(EnemyKillController.Update))]
    [HarmonyPostfix]
    private static void AfterEnemyFlightUpdate(EnemyKillController __instance, bool __state)
    {
        if (!__state || !Tracked(QuestSource.RagdollDistance))
            return;

        try
        {
            if (_instance.InRun() && __instance._distanceFlew != null && __instance._distanceFlew.Value >= 10f)
                _instance._client.ObserveQuestProgress(QuestSource.RagdollDistance, 1);
        }
        catch (Exception ex)
        {
            _instance._log.Write("Cannot track enemy ragdoll distance: " + ex.Message);
        }
    }

    [HarmonyPatch(typeof(RoomCompletionHistory), nameof(RoomCompletionHistory.SetCrushedReward))]
    [HarmonyPostfix]
    private static void OnRewardPicked(RoomCompletionHistory __instance, ArmamentAugment __0) =>
        Handle(
            "reward",
            () =>
            {
                if (__instance.levelManager?.Campaign?.GUID != StageChecks.CampaignGuid)
                    return;

                if (
                    !StageRewardRuntime.IsCompletingOffer
                    && (
                        Tracked(QuestSource.NoVitality)
                        || Tracked(QuestSource.NoResolve)
                        || Tracked(QuestSource.NoAetherReward)
                    )
                )
                    _instance._client.RecordRewardKindsUsed(
                        __0.TryCast<RestoreHealthAugment>() != null,
                        __0.TryCast<EmbeddedTraitAugment>()?.DeveloperName == "max_health_increase",
                        __0.TryCast<AetherMoteArmamentAugment>() != null
                    );
                HistoryChanged(__instance);
            }
        );

    [HarmonyPatch(typeof(RoomCompletionHistory), nameof(RoomCompletionHistory.SetCompletionState))]
    [HarmonyPostfix]
    private static void HistoryChanged(RoomCompletionHistory __instance) =>
        Handle(
            "aether",
            () =>
            {
                if (
                    !_instance._client.QuestRunActive
                    || !(Tracked(QuestSource.AetherRun) || Tracked(QuestSource.AetherTotal))
                    || __instance.levelManager?.Campaign?.GUID != StageChecks.CampaignGuid
                )
                    return;

                var total = 0;
                for (var index = 0; index < __instance.Count; index++)
                {
                    var room = __instance.GetRoomData(index);
                    total = checked(total + room.AetherRewardAmount + room.roomMotes + room.CalcEnemyPayout());
                }
                _instance._client.QuestAether(total);
            }
        );

    [HarmonyPatch(typeof(CashOutScreen), nameof(CashOutScreen.DoCashOutSave))]
    [HarmonyPrefix]
    private static void CashingOut(CashOutScreen __instance) =>
        Handle(
            "cash out",
            () =>
            {
                if (__instance.history?.levelManager?.Campaign?.GUID != StageChecks.CampaignGuid)
                    return;

                HistoryChanged(__instance.history);
                var seconds = __instance.runPlayTime.Value;
                _instance._log.Write(
                    $"Cash out nativeReward={__instance.totalReward} victory={__instance.isVictory} runSeconds={seconds}"
                );
                _instance._client.QuestRunFinished(
                    __instance.isVictory,
                    seconds,
                    GameDifficulty.OfRun(__instance.history.levelManager) ?? -1
                );
            }
        );

    [HarmonyPatch(typeof(RoomCompletionHistory), nameof(RoomCompletionHistory.QuickCashout))]
    [HarmonyPrefix]
    private static void QuickCashout(RoomCompletionHistory __instance) =>
        Handle(
            "quick cash out",
            () =>
            {
                if (__instance.levelManager?.Campaign?.GUID != StageChecks.CampaignGuid)
                    return;

                HistoryChanged(__instance);
                _instance._client.QuestRunFinished(false, __instance.levelManager.runPlayTime.Value);
            }
        );

    private static void RecordProjectileHit(Enemy? enemy, QuestSource source, float seconds = ProjectileKillWindow)
    {
        if (enemy == null || !Tracked(source))
            return;

        if (ProjectileHits.Count > 256)
            ProjectileHits.Clear();
        var until = UnityEngine.Time.time + seconds;
        if (ProjectileHits.TryGetValue(enemy.Pointer, out var known) && known.Until > until)
            return;

        ProjectileHits[enemy.Pointer] = (source, until);
    }

    [HarmonyPatch(
        typeof(Il2CppSG.Claymore.Status.SoulBurnHitProperty),
        nameof(Il2CppSG.Claymore.Status.SoulBurnHitProperty.Apply)
    )]
    [HarmonyPostfix]
    private static void SoulBurnApplied(Il2CppSG.Claymore.Status.SoulBurnHitProperty __instance, EnemyHealth health) =>
        Handle(
            QuestSource.SoulBurnKills,
            () =>
            {
                if (!Tracked(QuestSource.SoulBurnKills))
                    return;

                var seconds = __instance.duration + ProjectileKillWindow;
                _instance._log.Write(
                    $"Soul Burn applied duration={__instance.duration:F1}s tick={__instance.damageInterval:F1}s health={__instance.healthDamage}"
                );
                RecordProjectileHit(health?.Enemy, QuestSource.SoulBurnKills, seconds);
            }
        );

    [HarmonyPatch(
        typeof(Il2CppSG.Claymore.Armaments.Abilities.SoulburnProjectile),
        nameof(Il2CppSG.Claymore.Armaments.Abilities.SoulburnProjectile.OnProjectileHit)
    )]
    [HarmonyPrefix]
    private static void SoulburnHit(UnityEngine.Collider hitCollider) =>
        Handle(
            QuestSource.SoulBurnKills,
            () =>
            {
                if (Tracked(QuestSource.SoulBurnKills))
                    RecordProjectileHit(hitCollider?.GetComponentInParent<Enemy>(), QuestSource.SoulBurnKills);
            }
        );

    [HarmonyPatch(
        typeof(Il2CppSG.Claymore.Armaments.Abilities.ShardstormProjectile),
        nameof(Il2CppSG.Claymore.Armaments.Abilities.ShardstormProjectile.DoTick)
    )]
    [HarmonyPrefix]
    private static void ShardstormTick(Enemy e) =>
        Handle(QuestSource.ShardstormKills, () => RecordProjectileHit(e, QuestSource.ShardstormKills));

    [HarmonyPatch(typeof(EnemyKillController), nameof(EnemyKillController.Kill))]
    [HarmonyPrefix]
    private static void BeforeKill(EnemyKillController __instance, out bool __state) => __state = __instance._isDying;

    [HarmonyPatch(typeof(EnemyKillController), nameof(EnemyKillController.Kill))]
    [HarmonyPostfix]
    private static void AfterKill(EnemyKillController __instance, bool __state)
    {
        if (__state || !__instance._isDying)
            return;

        var crowd = Tracked(QuestSource.KillsInWindow);
        var projectile = ProjectileHits.Count > 0;
        var superKill = SuperKillQuests.Values.Any(Tracked);
        if (!crowd && !projectile && !superKill)
            return;

        try
        {
            var manager = _instance._level();
            if (manager == null || !_instance.InRun())
                return;

            if (crowd)
                _instance.CrowdKill();
            var victim = __instance.Enemy;
            if (victim != null && ProjectileHits.Remove(victim.Pointer, out var hit))
            {
                var left = hit.Until - UnityEngine.Time.time;
                if (left >= 0f)
                {
                    Count(hit.Source);
                    return;
                }
            }
            if (!superKill)
                return;

            foreach (var (weapon, active) in HeldSupers())
            {
                if (!SuperKillQuests.TryGetValue(weapon.Super, out var source))
                    continue;
                if (active)
                {
                    Count(source);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _instance._log.Write("Failed to read weapon super on kill: " + ex.Message);
        }
    }

    [HarmonyPatch(typeof(MeleeWeapon), nameof(MeleeWeapon.DoHit))]
    [HarmonyPostfix]
    private static void MeleeHit(MeleeWeapon __instance, ClaymoreEntity __0)
    {
        if (!Tracked(QuestSource.MultiHitSwings))
            return;

        Handle(QuestSource.MultiHitSwings, () => _instance.SweepHit(__instance, __0));
    }

    private void SweepHit(MeleeWeapon weapon, ClaymoreEntity entity)
    {
        var item = WeaponCatalog.ByGuid(weapon.Armament?.ID?.GUID ?? "");
        if (item == null || item.Name != QuestChecks.All[SweepQuest].Need.Weapons[0])
            return;

        var enemy = entity?.GetComponentInParent<Enemy>();
        if (enemy == null || !InRun())
            return;

        var now = UnityEngine.Time.time;
        var gap = now - _sweepTime;
        var samePair = _sweepWeapon == weapon.Pointer && _sweepEnemy != IntPtr.Zero && gap <= SweepWindow;
        if (samePair && _sweepEnemy == enemy.Pointer)
        {
            _log.Write($"Sweep: same enemy hit again gap={gap:0.000}s");
            return;
        }

        if (samePair)
        {
            _sweepEnemy = IntPtr.Zero;
            _log.Write($"Sweep counted: second enemy gap={gap:0.000}s");
            Count(QuestSource.MultiHitSwings);

            return;
        }

        _sweepWeapon = weapon.Pointer;
        _sweepEnemy = enemy.Pointer;
        _sweepTime = now;
        _log.Write($"Sweep: first enemy hit gap since previous={gap:0.000}s");
    }

    private void CrowdKill()
    {
        var count = QuestChecks.RecordKillInWindow(_recentKills, UnityEngine.Time.time, CrowdWindow);
        Handle(QuestSource.KillsInWindow, () => _client.ObserveQuestProgress(QuestSource.KillsInWindow, count));
    }

    private static IEnumerable<(WeaponItem Weapon, bool Active)> HeldSupers()
    {
        foreach (var hand in _instance._level().ActiveHands())
        {
            var armament = hand.BoundArmament;
            var weapon = WeaponCatalog.ByGuid(armament?.ID?.GUID ?? "");

            if (weapon != null)
                yield return (weapon, armament!.ability?.Ability?.IsActive ?? false);
        }
    }

    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Damage))]
    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.DealMassiveDamage))]
    [HarmonyPrefix]
    private static void BeforeDamage(PlayerHealth __instance, out float __state)
    {
        __state = __instance.CurrentHealth;
        _instance._wasOnDeathsDoor = __instance.IsOnDeathsDoor;
    }

    private static bool DamageTracked() => Tracked(QuestSource.BrinkOfRuin) || Tracked(QuestSource.Untouched);

    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.DealMassiveDamage))]
    [HarmonyPostfix]
    private static void AfterMassiveDamage(PlayerHealth __instance, float __state) =>
        AfterDamage(__instance, __state, 0f);

    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Damage))]
    [HarmonyPostfix]
    private static void AfterDamage(PlayerHealth __instance, float __state, float __result)
    {
        if (!DamageTracked())
            return;

        try
        {
            if (!_instance.InRun())
                return;

            if (!_instance._wasOnDeathsDoor && __instance.IsOnDeathsDoor)
            {
                _instance._deathsDoorEntries++;
                _instance._log.Write($"Entered near death state, entries this run={_instance._deathsDoorEntries}");
                _instance._client.ObserveQuestProgress(QuestSource.BrinkOfRuin, _instance._deathsDoorEntries);
            }
            if (__instance.CurrentHealth >= __state && __result <= 0f)
                return;
            _instance._actDamaged = true;
        }
        catch (Exception ex)
        {
            _instance._log.Write("Failed to read player damage: " + ex.Message);
        }
    }

    [HarmonyPatch(typeof(RoomCompletionHistory), nameof(RoomCompletionHistory.SetCompletionState))]
    [HarmonyPostfix]
    private static void StageCompleted(RoomCompletionHistory __instance, bool __0) =>
        Handle(
            QuestSource.Untouched,
            () =>
            {
                var manager = __instance.levelManager;
                if (
                    !Tracked(QuestSource.Untouched)
                    || manager == null
                    || !StageChecks.TryGetStage(manager.Campaign?.GUID, manager.LevelIndex, true, out var stage)
                )
                    return;
                if (!__0)
                {
                    _instance._actDamaged = false;

                    return;
                }

                _instance._cleanStreak = _instance._actDamaged ? 0 : _instance._cleanStreak + 1;
                _instance._log.Write(
                    $"Stage {stage} cleared damaged={_instance._actDamaged} cleanStreak={_instance._cleanStreak}"
                );
                _instance._client.ObserveQuestProgress(QuestSource.Untouched, _instance._cleanStreak);
                _instance._actDamaged = false;
            }
        );

    [HarmonyPatch(typeof(ArmamentInventory), nameof(ArmamentInventory.BuyArmament), typeof(ArmamentID), typeof(int))]
    [HarmonyPostfix]
    private static void Bought(int __1, bool __result)
    {
        _instance._log.Write($"BuyArmament level={__1} result={__result}");
        if (!__result || __1 < 1)
            return;

        if (Tracked(QuestSource.WeaponUpgraded))
            Count(QuestSource.WeaponUpgraded);
        if (__1 >= MasterworkLevel && Tracked(QuestSource.WeaponMaxed))
            Count(QuestSource.WeaponMaxed);
    }
}
