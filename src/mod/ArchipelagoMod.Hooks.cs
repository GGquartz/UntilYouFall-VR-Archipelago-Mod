using HarmonyLib;
using Il2CppSG.Claymore.Entities;
using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.PlayerLoopControllers;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using UntilYouFall.Shared;

namespace UntilYouFallArchipelago;

public sealed partial class ArchipelagoMod
{
    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Start))]
    [HarmonyPostfix]
    private static void OnPlayerHealthStart(PlayerHealth __instance) => _instance._playerHealth = __instance;

    private string FormatStageRewardContextForLog() =>
        _levelManager == null
            ? "profile=unknown seed=unknown index=unknown room=unknown"
            : $"profile={_levelManager.saveManager?.ProfileIndex} {_levelManager.DescribeForLog()}";

    [HarmonyPatch(
        typeof(ScreenFadeController),
        nameof(ScreenFadeController.FadeTo),
        typeof(float),
        typeof(Color),
        typeof(Il2CppSystem.Action)
    )]
    [HarmonyPrefix]
    private static void PrepareRuneforgeBeforeFadeIn(ScreenFadeController __instance, Color __1)
    {
        try
        {
            var manager = _instance._levelManager;
            if (
                manager == null
                || manager.fadeController?.Pointer != __instance.Pointer
                || !manager.IsLoading
                || manager.IsCoreLoading
                || __1.a != 0f
            )
                return;

            _instance._runeforgeClutter.BeforeFadeIn(manager);
            _instance._questBoard.BeforeFadeIn(manager);
            _instance._runeforgeShop.BeforeFadeIn(manager);
        }
        catch (Exception exception)
        {
            _instance.Channel("APRuneforgeClutter").Warn("Before fade hook failed: " + exception);
        }
    }

    [HarmonyPatch(typeof(LevelManager), nameof(LevelManager.StartLoad))]
    [HarmonyPrefix]
    private static void OnRoomLoad(LevelManager __instance, Campaign.RoomSelection level)
    {
        var combatRoom = level.Encounter != null && level.Room?.GUID != RuneforgeShopRules.RuneforgeRoomGuid;
        if (!combatRoom)
            _instance._apClient.VoidRunOnlyEffects();
        _instance._enemyStasis.RoomLoad(combatRoom);
        _instance._noBlockingTrap.RoomLoad(combatRoom);
        _instance._exhaustionTrap.RoomLoad(combatRoom);
        _instance._cursedTrap.RoomLoad(combatRoom);
        _instance._levelManager = __instance;
        _instance._playerHealth = null;
        _instance._consoleUi.SetCombatHidden(level.Encounter != null);
        _instance
            .Channel("RoomLoad")
            .Write(
                $"seed={__instance.CurrentSeed} index={__instance.LevelIndex} campaign={__instance.Campaign?.GUID} room={level.Room?.DeveloperName} roomGuid={level.Room?.GUID} encounterGuid={level.Encounter?.GUID} boss={level.Room?.IsBossRoom}"
            );
    }

    [HarmonyPatch(typeof(Il2Cpp.RoomCompletionHistory), nameof(Il2Cpp.RoomCompletionHistory.SetCompletionState))]
    [HarmonyPostfix]
    private static void OnRoomCompletion(Il2Cpp.RoomCompletionHistory __instance, bool success)
    {
        var manager = __instance.levelManager;
        if (manager == null || manager.LevelIndex < 0)
            return;

        if (manager.Pointer == _instance._levelManager?.Pointer)
        {
            if (success)
            {
                _instance._enemyStasis.RoomCleared();
                _instance._noBlockingTrap.RoomCleared();
                _instance._exhaustionTrap.RoomCleared();
                _instance._cursedTrap.RoomCleared();
            }
            else
            {
                _instance._apClient.VoidRunOnlyEffects();
                _instance._enemyStasis.EndRun("run-failed");
                _instance._noBlockingTrap.EndRun("run-failed");
                _instance._exhaustionTrap.EndRun("run-failed");
                _instance._cursedTrap.EndRun("run-failed");
            }
        }
        if (!success && manager.Pointer == _instance._levelManager?.Pointer)
            _instance._enemySpawnTrap.RunEnded();

        _instance
            .Channel(success ? "RoomCompleted" : "RoomFailed")
            .Write($"source=SetCompletionState {manager.DescribeForLog()}");
        RecordStageClear(manager, success);
    }

    private static void RecordStageClear(LevelManager manager, bool success)
    {
        if (StageChecks.TryGetStage(manager.Campaign?.GUID, manager.LevelIndex, success, out var stage))
        {
            try
            {
                _instance._startingTraitRewards.StageCleared(manager, stage);
            }
            catch (Exception ex)
            {
                _instance.Channel("APStartingTraits").Error("Cannot record stage clear: " + ex.Message);
            }
            try
            {
                var difficulty = GameDifficulty.OfRun(manager);
                if (difficulty == null)
                    _instance.Channel("APDifficultyStage").Warn($"Stage {stage}: campaign database unavailable");
                else
                    _instance._apClient.ClearStageDifficulty(stage, difficulty.Value);
            }
            catch (Exception ex)
            {
                _instance.Channel("APDifficultyStage").Error("Cannot record stage: " + ex.Message);
            }
            try
            {
                var inventory = manager.armamentInventory;
                var guids = new[] { inventory?.leftHand?.Armament?.GUID, inventory?.rightHand?.Armament?.GUID };
                foreach (var guid in guids)
                {
                    if (string.IsNullOrWhiteSpace(guid))
                        _instance.Channel("APStage").Write($"Stage {stage}: empty hand slot ignored");
                    else if (WeaponCatalog.ByGuid(guid) == null)
                        _instance.Channel("APStage").Warn($"Stage {stage}: unknown weapon ignored: {guid}");
                }
                if (!string.IsNullOrWhiteSpace(guids[0]) && guids[0] == guids[1])
                    _instance.Channel("APStage").Warn($"Stage {stage}: SAME weapon in both hands guid={guids[0]}");

                var weapons = StageChecks.ResolveWeapons(guids);
                _instance._apClient.ClearStage(stage, weapons);
                _instance._apClient.QuestClear(stage, weapons.Select(weapon => weapon.Name).ToArray());

                _instance.Channel("APStage").Write($"Stage {stage}: resolved weapons={weapons.Count}");
            }
            catch (Exception ex)
            {
                _instance.Channel("APStage").Error("Cannot record stage: " + ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.OnLastEnemyKilled))]
    [HarmonyPostfix]
    private static void OnEnemiesCleared(PlayerHealth __instance)
    {
        var manager = _instance._levelManager;
        if (manager != null && _instance._playerHealth?.Pointer == __instance.Pointer)
        {
            _instance._enemyStasis.RoomCleared();
            _instance._enemySpawnTrap.RoomEnemiesCleared();
            _instance._noBlockingTrap.RoomCleared();
            _instance._exhaustionTrap.RoomCleared();
            _instance._cursedTrap.RoomCleared();
            _instance
                .Channel("RoomEnemiesCleared")
                .Write($"{manager.DescribeForLog()}; HP={__instance.CurrentHealth:F1}/{__instance.MaxHealth:F1}");
            _instance._consoleUi.SetCombatHidden(false);
            RecordStageClear(manager, true);
        }
    }

    [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.OnDestroy))]
    [HarmonyPrefix]
    private static void ForgetPlayerHealth(PlayerHealth __instance)
    {
        if (_instance._playerHealth?.Pointer == __instance.Pointer)
        {
            _instance._enemyStasis.PlayerDestroyed(__instance);
            _instance._noBlockingTrap.PlayerDestroyed(__instance);
            _instance._exhaustionTrap.PlayerDestroyed(__instance);
            _instance._cursedTrap.PlayerDestroyed(__instance);
            _instance._playerHealth = null;
        }
    }
}
