using System.Reflection;
using HarmonyLib;
using Il2CppSG.Claymore.Entities;
using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.PlayerLoopControllers;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using UntilYouFall.Shared;

[assembly: MelonInfo(
    typeof(UntilYouFallArchipelago.ArchipelagoMod),
    "Archipelago mod",
    UntilYouFallArchipelago.ModMetadata.Version,
    "https://github.com/GGquartz/"
)]
[assembly: MelonGame("Schell Games", "UntilYouFall")]
[assembly: AssemblyVersion(UntilYouFallArchipelago.ModMetadata.Version)]
[assembly: AssemblyFileVersion(UntilYouFallArchipelago.ModMetadata.Version)]
[assembly: AssemblyInformationalVersion(UntilYouFallArchipelago.ModMetadata.Version)]

namespace UntilYouFallArchipelago;

public sealed partial class ArchipelagoMod : MelonMod
{
    private static ArchipelagoMod _instance = null!;
    private MelonPreferences_Entry<string> _apServer = null!;
    private MelonPreferences_Entry<string> _apSlot = null!;

    private PlayerVitality _playerVitality = null!;
    private ArchipelagoClient _apClient = null!;
    private ApNotificationUi _noticeUi = null!;
    private DeathLinkController _deathLink = null!;
    private ApWristConsole _consoleUi = null!;
    private WeaponUnlocks _weaponUnlocks = null!;
    private DifficultyUnlocks _difficultyUnlocks = null!;
    private TutorialSkip _tutorialSkip = null!;
    private ProfileModStatus _profileModStatus = null!;
    private PauseModStatus _pauseModStatus = null!;
    private ExtraTraitSlots _extraTraitSlots = null!;
    private WeaponAbilityLocks _weaponAbilityLocks = null!;
    private WeaponUpgradeCap _weaponUpgradeCap = null!;
    private AetherFiller _aetherFiller = null!;
    private AetherMultiplier _aetherMultiplier = null!;
    private AetherMultiplierHubLabel _aetherMultiplierHubLabel = null!;
    private PlayerBonuses _playerBonuses = null!;
    private RuneforgeShop _runeforgeShop = null!;
    private RuneforgeClutterRemoval _runeforgeClutter = null!;
    private StartingTraitRewards _startingTraitRewards = null!;
    private StageRewardRuntime _stageRewardRuntime = null!;
    private EnemySpawnTrap _enemySpawnTrap = null!;
    private EnemyStasis _enemyStasis = null!;
    private SuperCharge _superCharge = null!;
    private NoBlockingTrap _noBlockingTrap = null!;
    private ExhaustionTrap _exhaustionTrap = null!;
    private CursedTrap _cursedTrap = null!;
    private QuestBoard _questBoard = null!;
    private EliteEncounterTrap _eliteEncounterTrap = null!;
    private ItemEffects? _itemEffects;
    private bool _rewardScaleApplied;
    private string _apCommandPath = "";
    private string _apStatusPath = "";

    private string _apPassword = "";

    private void WriteApStatus(ConnectionState state, string detail)
    {
        try
        {
            AtomicFile.Write(
                _apStatusPath,
                temporary =>
                    File.WriteAllText(
                        temporary,
                        state.ToString().ToLowerInvariant() + "\n" + detail.Replace("\r", " ").Replace("\n", " ")
                    )
            );
        }
        catch (Exception ex)
        {
            Channel("AP").Write("Status file failed: " + ex.Message);
        }
    }

    private PlayerHealth? _playerHealth;
    private LevelManager? _levelManager;
    private long _nextRequestPollAt;
    private long _nextNoticeUiUpdateAt;
    private const long NoticeUiUpdateIntervalMs = 100;

    private static bool ConsumeDebugMarker(string fileName)
    {
        var marker = Path.Combine(MelonEnvironment.UserDataDirectory, fileName);
        if (!File.Exists(marker))
            return false;

        File.Delete(marker);
        return true;
    }

    public override void OnSceneWasLoaded(int buildIndex, string sceneName) =>
        Channel("AP").Write($"Scene loaded: {sceneName} (build {buildIndex})");

    private LogChannel Channel(string category) =>
        new(
            message => LoggerInstance.Msg(message),
            category,
            message => LoggerInstance.Warning(message),
            message => LoggerInstance.Error(message)
        );

    public override void OnInitializeMelon()
    {
        _instance = this;
        Action<string> log = message => LoggerInstance.Msg(message);
        LogArchive.Run(log);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            log.Write("AP", $"UNHANDLED EXCEPTION (terminating={args.IsTerminating}): {args.ExceptionObject}");

        _playerVitality = new PlayerVitality(() => _playerHealth, () => _levelManager, log);

        var preferences = MelonPreferences.CreateCategory(LauncherProtocol.PreferencesSection);
        _apServer = preferences.CreateEntry("APServer", "");
        _apSlot = preferences.CreateEntry("APSlot", "");
        _noticeUi = new ApNotificationUi(log);
        _consoleUi = new ApWristConsole(() => _levelManager, log);
        _apClient = new ArchipelagoClient(
            Path.Combine(MelonEnvironment.UserDataDirectory, "Archipelago", "state"),
            log,
            _noticeUi.Enqueue,
            _consoleUi.Add,
            _playerVitality.CanHeal
        );
        _apCommandPath = Path.Combine(MelonEnvironment.UserDataDirectory, LauncherProtocol.CommandFile);
        File.Delete(_apCommandPath);
        _apStatusPath = Path.Combine(MelonEnvironment.UserDataDirectory, LauncherProtocol.StatusFile);
        File.Delete(_apStatusPath);
        _apClient.StatusChanged = WriteApStatus;
        _apClient.ConnectionLost = () => _noticeUi.ShowTechnical(ApNotice.Error("Disconnected from AP Server"));

        var debugEnabled = ConsumeDebugMarker("Archipelago.debug.enabled");
        DebugWeaponDamage.Install(HarmonyInstance, debugEnabled, log);
        DebugAether.Install(HarmonyInstance, debugEnabled, log);
        _weaponUnlocks = new WeaponUnlocks(HarmonyInstance, log, _noticeUi.ShowTechnical, () => _apClient);
        _difficultyUnlocks = new DifficultyUnlocks(
            HarmonyInstance,
            log,
            () => _apClient?.GoalProgressText(),
            tier => _apClient?.DifficultyStageProgress(tier)
        );
        _extraTraitSlots = new ExtraTraitSlots(HarmonyInstance, log);
        _weaponAbilityLocks = new WeaponAbilityLocks(HarmonyInstance, log);
        _ = new ClosedCaptionFix(HarmonyInstance, log);
        ApRewardInspectionPanel.Install(HarmonyInstance);
        EmptyHandInspection.Install(HarmonyInstance, log);
        _weaponUpgradeCap = new WeaponUpgradeCap(HarmonyInstance, log, _noticeUi.ShowTechnical);
        _aetherFiller = new AetherFiller(log);
        AetherFiller.CurrentLevel = () => _levelManager;
        _aetherMultiplier = new AetherMultiplier(HarmonyInstance, () => _apClient.AetherMultiplierItemCount, log);
        _aetherMultiplierHubLabel = new AetherMultiplierHubLabel(() => _apClient.AetherMultiplierItemCount, log);
        _questBoard = new QuestBoard(log, _apClient, _noticeUi.ShowTechnical);
        _playerBonuses = new PlayerBonuses(HarmonyInstance, log);
        SuperDuplicateGuard.Install(HarmonyInstance, log);
        SuperMaxGuard.Install(HarmonyInstance, log);
        PlayerBonuses.StageProgress = weaponId => _apClient?.WeaponStageProgress(weaponId);
        PlayerBonuses.WeaponLocked = _weaponUnlocks.IsLocked;
        _runeforgeClutter = new RuneforgeClutterRemoval(log);
        _runeforgeShop = new RuneforgeShop(
            HarmonyInstance,
            new RuneforgeRackCloner(log),
            _apClient,
            _noticeUi.ShowTechnical,
            log
        );
        _startingTraitRewards = new StartingTraitRewards(HarmonyInstance, _apClient, log);
        _stageRewardRuntime = new StageRewardRuntime(
            HarmonyInstance,
            _apClient,
            FormatStageRewardContextForLog,
            log,
            debugEnabled
        );
        _enemySpawnTrap = new EnemySpawnTrap(HarmonyInstance, () => _levelManager, log, _noticeUi.ShowTechnical);
        _noBlockingTrap = new NoBlockingTrap(
            HarmonyInstance,
            () => _playerHealth,
            _playerVitality.CanHeal,
            () => _levelManager,
            () => _apClient.TrapEffectLengthSeconds,
            log
        );
        _exhaustionTrap = new ExhaustionTrap(
            HarmonyInstance,
            () => _playerHealth,
            _playerVitality.CanHeal,
            () => _levelManager,
            () => _noBlockingTrap.Active,
            () => _apClient.TrapEffectLengthSeconds,
            log
        );
        _cursedTrap = new CursedTrap(
            HarmonyInstance,
            () => _playerHealth,
            _playerVitality.CanHeal,
            () => _levelManager,
            () => TimedTrapHudLayout.CursedRow(_noBlockingTrap.Active, _exhaustionTrap.Active),
            () => _apClient.TrapEffectLengthSeconds,
            log
        );
        _enemyStasis = new EnemyStasis(
            HarmonyInstance,
            () => _playerHealth,
            () => _levelManager,
            _playerVitality.CanHeal,
            () => TimedTrapHudLayout.StasisRow(_noBlockingTrap.Active, _exhaustionTrap.Active, _cursedTrap.Active),
            log
        );
        _superCharge = new SuperCharge(
            () => _playerHealth,
            () => _levelManager,
            _playerVitality.CanHeal,
            _weaponAbilityLocks,
            log
        );
        _eliteEncounterTrap = new EliteEncounterTrap(
            HarmonyInstance,
            _apClient,
            Path.Combine(MelonEnvironment.UserDataDirectory, "Archipelago", "elite-room-overrides.json"),
            log
        );
        _ = new SingleWeaponAnalytics(HarmonyInstance, () => _weaponUnlocks.HasApLoadout, log);
        _ = new SingleWeaponRewards(HarmonyInstance, () => _weaponUnlocks.HasApLoadout, log);
        _ = new SingleWeaponHud(
            HarmonyInstance,
            () => _weaponUnlocks.HasApLoadout,
            guid => _weaponAbilityLocks.Locked(guid),
            log
        );
        _ = new ProfileWeaponIcons(HarmonyInstance, log);
        _profileModStatus = new ProfileModStatus(HarmonyInstance, () => _apClient.Connected, log);
        _pauseModStatus = new PauseModStatus(HarmonyInstance, () => _apClient.Connected, log);
        _tutorialSkip = new TutorialSkip(HarmonyInstance, () => _apClient.Connected, log);
        _ = new EnemyKillChecks(
            HarmonyInstance,
            () => _levelManager,
            () => _playerHealth != null && !_playerHealth.IsDead,
            _apClient.FirstKill,
            enemy => _apClient.QuestKill(enemy, (_levelManager?.LevelIndex ?? -1) + 1),
            log
        );

        _deathLink = new DeathLinkController(
            HarmonyInstance,
            _apClient,
            () => _levelManager,
            () => _playerHealth,
            _playerVitality.CanHeal,
            _noticeUi.Enqueue,
            [
                (ApItems.EnemySpawnTrapName, _enemySpawnTrap.Spawn),
                (ApItems.DodgeOnlyTrapName, _noBlockingTrap.Receive),
                (ApItems.ExhaustionTrapName, _exhaustionTrap.Receive),
                (ApItems.EliteEncounterTrapName, _apClient.RegisterDeathLinkEliteTrap),
                (ApItems.CursedTrapName, _cursedTrap.Receive),
            ],
            log
        );
        _ = new QuestEventHooks(HarmonyInstance, _apClient, () => _levelManager, log);

        HarmonyInstance.Patch(
            AccessTools.Method(typeof(PlayerHealth), nameof(PlayerHealth.Start)),
            postfix: new HarmonyMethod(typeof(ArchipelagoMod), nameof(OnPlayerHealthStart))
        );
        HarmonyInstance.Patch(
            AccessTools.Method(typeof(LevelManager), nameof(LevelManager.StartLoad)),
            prefix: new HarmonyMethod(typeof(ArchipelagoMod), nameof(OnRoomLoad))
        );
        HarmonyInstance.Patch(
            AccessTools.Method(
                typeof(ScreenFadeController),
                nameof(ScreenFadeController.FadeTo),
                [typeof(float), typeof(Color), typeof(Il2CppSystem.Action)]
            ),
            prefix: new HarmonyMethod(typeof(ArchipelagoMod), nameof(PrepareRuneforgeBeforeFadeIn))
        );
        HarmonyInstance.Patch(
            AccessTools.Method(
                typeof(Il2Cpp.RoomCompletionHistory),
                nameof(Il2Cpp.RoomCompletionHistory.SetCompletionState)
            ),
            postfix: new HarmonyMethod(typeof(ArchipelagoMod), nameof(OnRoomCompletion))
        );
        HarmonyInstance.Patch(
            AccessTools.Method(typeof(PlayerHealth), nameof(PlayerHealth.OnLastEnemyKilled)),
            postfix: new HarmonyMethod(typeof(ArchipelagoMod), nameof(OnEnemiesCleared))
        );
        HarmonyInstance.Patch(
            AccessTools.Method(typeof(PlayerHealth), nameof(PlayerHealth.OnDestroy)),
            prefix: new HarmonyMethod(typeof(ArchipelagoMod), nameof(ForgetPlayerHealth))
        );

        LoggerInstance.Msg($"Archipelago mod loaded (v{ModMetadata.Version})");
    }

    private void UpdateNoticeUi()
    {
        var now = Environment.TickCount64;
        if (now < _nextNoticeUiUpdateAt)
            return;
        _nextNoticeUiUpdateAt = now + NoticeUiUpdateIntervalMs;
        _noticeUi.Update();
    }

    public override void OnUpdate()
    {
        _questBoard.Update(_levelManager);
        _startingTraitRewards.Update();
        UpdateNoticeUi();
        _enemySpawnTrap.Update();
        _enemyStasis.Update();
        _noBlockingTrap.Update();
        _exhaustionTrap.Update();
        _cursedTrap.Update();
        _runeforgeClutter.Update(_levelManager);

        var now = Environment.TickCount64;
        if (now < _nextRequestPollAt)
            return;
        _nextRequestPollAt = now + 250;
        DebugWeaponDamage.Update();

        try
        {
            _apClient.Update();
            _profileModStatus.Update();
            _pauseModStatus.Update();
            _aetherMultiplierHubLabel.Update(_levelManager);
            _runeforgeShop.Update(_levelManager);
            _stageRewardRuntime.Update();
            _deathLink.Update();
            _eliteEncounterTrap.Update();
            _extraTraitSlots.Update(_apClient.StartingHands.HasValue ? _apClient.TraitSlotUnlocks : null);
            _weaponAbilityLocks.Update(
                _apClient.StartingHands.HasValue && _apClient.SeparateWeaponSupers,
                _apClient.WeaponAbilityUnlocks
            );
            _weaponUpgradeCap.Update(
                _apClient.StartingHands.HasValue ? _apClient.WeaponUpgradeItemCount : null,
                _levelManager != null && !_levelManager.HasEncounter()
            );
            _playerBonuses.Update(
                _levelManager?.armamentInventory,
                _apClient.StartingHands.HasValue ? _apClient.PlayerBonusItemCounts : null
            );
            _tutorialSkip.Update(_levelManager);
            _difficultyUnlocks.Update(_levelManager, _apClient.DifficultyConfig, _apClient.DifficultyUnlocks);
            _weaponUnlocks.SyncState(
                _apClient.WeaponUnlocks,
                _apClient.StartingWeaponIds,
                _apClient.StartingHands,
                _apClient.HandUnlocks
            );
            if (_levelManager != null && !_levelManager.IsRoomLoading())
                _weaponUnlocks.Update(_levelManager);
            _itemEffects ??= new ItemEffects
            {
                CanHeal = _playerVitality.CanHeal,
                Heal = _playerVitality.Restore,
                ApplyAetherFiller = (index, amount) => _aetherFiller.TryApply(index, amount),
                CanSpawnEnemy = () => _levelManager?.IsRoomLoading() != true,
                SpawnEnemy = _enemySpawnTrap.Spawn,
                DodgeOnlyTrap = _noBlockingTrap.Receive,
                ExhaustionTrap = _exhaustionTrap.Receive,
                CursedTrap = _cursedTrap.Receive,
                SuperCharge = _superCharge.Apply,
                CanApplySuperCharge = _superCharge.Ready,
                EnemyStasis = _enemyStasis.Receive,
            };
            _apClient.ApplyNextItem(_itemEffects);
            if (!_rewardScaleApplied)
            {
                AetherRewardScale.Apply(message => LoggerInstance.Msg(message));
                _rewardScaleApplied = true;
            }
            ProcessApCommand();
        }
        catch (Exception ex)
        {
            Channel("AP").Error("Command/update failed: " + ex.Message);
            _apClient.Disconnect();
        }
    }

    public override void OnLateUpdate() => _consoleUi.Update();

    public override void OnDeinitializeMelon()
    {
        _difficultyUnlocks.Dispose();
        _startingTraitRewards.Dispose();
        _enemyStasis.EndRun("shutdown");
        _noBlockingTrap.EndRun("shutdown");
        _exhaustionTrap.EndRun("shutdown");
        _cursedTrap.EndRun("shutdown");
        _apClient.Disconnect();
        _noticeUi.Dispose();
        _consoleUi.Dispose();
        _aetherMultiplierHubLabel.Dispose();
        _runeforgeShop.Dispose();
        _runeforgeClutter.Dispose();
        _stageRewardRuntime.Dispose();
        _enemySpawnTrap.Dispose();
        _eliteEncounterTrap.Dispose();
    }
}
