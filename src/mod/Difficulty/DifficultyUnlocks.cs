using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSG.Claymore.Armaments;
using Il2CppSG.Claymore.Interaction;
using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.SaveSystem;
using Il2CppSG.Claymore.SaveSystem.SavedElements;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal sealed class DifficultyUnlocks : IDisposable
{
    private static DifficultyUnlocks _instance = null!;
    private readonly LogChannel _log;
    private readonly LogChannel _stageProgressLog;
    private readonly Func<string?> _goalText;
    private readonly Func<int, string?> _stageProgress;
    private readonly HashSet<(int Tier, string Progress)> _stageProgressDiagnostics = [];
    private readonly HashSet<(IntPtr Manager, int Profile)> _freshProfiles = [];
    private DifficultySettings? _settings;
    private HashSet<long> _owned = [];
    private IntPtr _loadingManager;
    private int _loadingProfile = -1;
    private long _nextRefreshAt;
    private ArmamentVisualPreview? _ghostStyle;
    private bool _waitingForStyle;
    private readonly Dictionary<
        IntPtr,
        (
            CrushableVisuals Visuals,
            List<(Renderer Renderer, Il2CppReferenceArray<Material> Originals, Material[] Copies)> Materials
        )
    > _ghosts = [];

    public DifficultyUnlocks(
        HarmonyLib.Harmony harmony,
        Action<string> log,
        Func<string?> goalText,
        Func<int, string?> stageProgress
    )
    {
        _goalText = goalText;
        _stageProgress = stageProgress;
        _instance = this;
        _log = log.Channel("APDifficulty");
        _stageProgressLog = log.Channel("APStageProgress");
        harmony.Patch(
            AccessTools.PropertyGetter(typeof(CrushInteractable), nameof(CrushInteractable.CanCrush)),
            prefix: new HarmonyMethod(typeof(DifficultyUnlocks), nameof(CanCrush))
        );
        foreach (
            var method in new[] { nameof(CrushInteractable.FinishCrush), nameof(CrushInteractable.ProxyCrushComplete) }
        )
            harmony.Patch(
                AccessTools.Method(typeof(CrushInteractable), method),
                prefix: new HarmonyMethod(typeof(DifficultyUnlocks), nameof(AllowCompletion))
            );
        harmony.Patch(
            AccessTools.Method(typeof(DifficultyInteractable), nameof(DifficultyInteractable.OnCrushComplete)),
            prefix: new HarmonyMethod(typeof(DifficultyUnlocks), nameof(AllowCompletion))
        );
        harmony.Patch(
            AccessTools.PropertyGetter(
                typeof(DifficultyInteractable),
                nameof(DifficultyInteractable.DisplayDescription)
            ),
            postfix: new HarmonyMethod(typeof(DifficultyUnlocks), nameof(ExtendStoneDescription))
        );
        foreach (
            var method in new[] { nameof(CrushInteractable.OnResourceLoadComplete), nameof(CrushInteractable.OnEnable) }
        )
            harmony.Patch(
                AccessTools.Method(typeof(CrushInteractable), method),
                postfix: new HarmonyMethod(typeof(DifficultyUnlocks), nameof(RefreshLoadedStone))
            );
        harmony.Patch(
            AccessTools.Method(typeof(DifficultyCompletionToggler), "OnEnable"),
            prefix: new HarmonyMethod(typeof(DifficultyUnlocks), nameof(ShowNightmare))
        );
        harmony.Patch(
            AccessTools.Method(typeof(SaveManager), nameof(SaveManager.LoadProfile)),
            prefix: new HarmonyMethod(typeof(DifficultyUnlocks), nameof(BeforeProfileLoad)),
            finalizer: new HarmonyMethod(typeof(DifficultyUnlocks), nameof(AfterProfileLoad))
        );
        harmony.Patch(
            AccessTools.Method(typeof(SavedInt), nameof(SavedInt.ApplyDefault)),
            postfix: new HarmonyMethod(typeof(DifficultyUnlocks), nameof(ProfileDefaulted))
        );
        harmony.Patch(
            AccessTools.Method(typeof(LevelManager), nameof(LevelManager.StartRun)),
            prefix: new HarmonyMethod(typeof(DifficultyUnlocks), nameof(BeforeRun))
        );
    }

    private bool Locked(DifficultyInteractable? stone) =>
        _settings != null
        && stone?._selectedDifficulty != null
        && !_settings.Allows(stone._selectedDifficulty.Value, _owned);

    private static bool CanCrush(CrushInteractable __instance, ref bool __result)
    {
        if (!_instance.Locked(__instance.TryCast<DifficultyInteractable>()))
            return true;

        __result = false;
        return false;
    }

    private static bool AllowCompletion(CrushInteractable __instance)
    {
        var stone = __instance.TryCast<DifficultyInteractable>();
        if (!_instance.Locked(stone))
            return true;

        __instance.OnCancelCrush();
        __instance.OnCrushAttemptFailed();

        _instance._log.Write($"Blocked locked stone tier={stone!._selectedDifficulty.Value}");
        return false;
    }

    private static void ExtendStoneDescription(DifficultyInteractable __instance, ref string __result)
    {
        var level = __instance._selectedDifficulty?.Value ?? -1;
        if (level is >= 0 and < 4)
            __result = AetherMultiplierRules.RewriteGain(__result, level);
        if (_instance.Locked(__instance) && !_instance._ghosts.ContainsKey(__instance.Pointer))
        {
            var tier = __instance._selectedDifficulty.Value;
            __result += _instance._settings!.InRange(tier)
                ? $"\nLocked - requires Difficulty: {DifficultySettings.Names[tier]}"
                : "\nUnavailable in this difficulty range";
        }
        var progress = _instance._stageProgress(level);
        if (progress != null)
        {
            __result += $"\n{ApColors.Rich(ApColors.Gold, progress)}";
            if (_instance._stageProgressDiagnostics.Add((level, progress)))
                _instance._stageProgressLog.Write($"Difficulty detail tier={level} {progress}");
        }
        var goal = _instance._goalText();
        if (goal != null)
            __result += $"\n{ApColors.Rich(ApColors.Gold, $"Goal: {goal}")}";
    }

    private static void RefreshLoadedStone(CrushInteractable __instance)
    {
        var stone = __instance.TryCast<DifficultyInteractable>();
        if (stone != null)
            _instance.RefreshGhost(stone);
    }

    private void RefreshGhost(DifficultyInteractable stone)
    {
        if (!stone.gameObject.scene.isLoaded)
            return;

        if (_ghosts.TryGetValue(stone.Pointer, out var current))
        {
            if (Locked(stone) && current.Visuals != null && stone.Visuals?.Pointer == current.Visuals.Pointer)
                return;

            RestoreGhost(stone.Pointer);
        }

        if (!Locked(stone))
            return;
        var renderers = stone.Visuals?.Renderers;
        if (renderers == null || renderers.Length == 0)
            return;

        if (_ghostStyle == null || _ghostStyle.unownedShader == null)
        {
            _ghostStyle = SceneCache.GhostStyle();
            if (_ghostStyle == null)
            {
                if (!_waitingForStyle)
                    _log.Write("Ghost waiting for native ghost preview");
                _waitingForStyle = true;

                return;
            }
            _waitingForStyle = false;
            _log.Write($"Ghost style preview={_ghostStyle.name} shader={_ghostStyle.unownedShader.name}");
        }
        var materials = new List<(Renderer Renderer, Il2CppReferenceArray<Material> Originals, Material[] Copies)>();
        _ghosts.Add(stone.Pointer, (stone.Visuals!, materials));
        try
        {
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                    continue;
                var originals = renderer.sharedMaterials;
                if (originals == null || !originals.Any(original => original != null))
                    continue;

                var copies = new Material[originals.Length];
                materials.Add((renderer, originals, copies));
                var replacement = new Il2CppReferenceArray<Material>(originals.Length);
                for (var index = 0; index < originals.Length; index++)
                {
                    if (originals[index] == null)
                        continue;

                    var material = new Material(originals[index]);
                    copies[index] = material;
                    material.shader = _ghostStyle.unownedShader;
                    foreach (var color in _ghostStyle.unownedColors)
                        material.SetColor(color.propertyName, color.color);
                    replacement[index] = material;
                }
                renderer.sharedMaterials = replacement;
            }
            if (materials.Count == 0)
                RestoreGhost(stone.Pointer);
            else
                _log.Write(
                    $"Ghost applied tier={stone._selectedDifficulty.Value} visuals={stone.Visuals!.name} renderers={materials.Count}"
                );
        }
        catch (Exception exception)
        {
            RestoreGhost(stone.Pointer);
            _log.Write($"Ghost failed: {exception}");
        }
    }

    private void RestoreGhost(IntPtr stone)
    {
        if (!_ghosts.Remove(stone, out var ghost))
            return;

        foreach (var (renderer, originals, copies) in ghost.Materials)
        {
            if (renderer != null)
                renderer.sharedMaterials = originals;
            foreach (var material in copies)
                if (material != null)
                    Object.Destroy(material);
        }

        _log.Write($"Ghost restored stone={stone}");
    }

    public void Dispose()
    {
        foreach (var stone in _ghosts.Keys.ToArray())
            RestoreGhost(stone);
    }

    private static bool ShowNightmare(DifficultyCompletionToggler __instance)
    {
        if (_instance._settings == null || __instance.objectToToggle == null || !__instance.gameObject.scene.IsValid())
            return true;

        for (var parent = __instance.transform; parent != null; parent = parent.parent)
        {
            var stones = parent.GetComponentsInChildren<DifficultyInteractable>(true);
            if (stones.Length == 0)
                continue;
            if (stones.Length != 1 || stones[0]._selectedDifficulty?.Value != 3)
                return true;
            __instance.objectToToggle.SetActive(_instance._settings.InRange(3));
            return false;
        }

        return true;
    }

    private static void BeforeProfileLoad(SaveManager __instance, int __0)
    {
        _instance._loadingManager = __instance.Pointer;
        _instance._loadingProfile = __0;
    }

    private static void AfterProfileLoad() => _instance._loadingProfile = -1;

    private static void ProfileDefaulted(SavedInt __instance)
    {
        if (_instance._loadingProfile >= 0 && GameDifficulty.IsSettingVariable(__instance.variable))
            _instance._freshProfiles.Add((_instance._loadingManager, _instance._loadingProfile));
    }

    private static void BeforeRun(LevelManager __instance) => _instance.ApplyStarting(__instance);

    public void Update(LevelManager? manager, DifficultySettings? settings, IReadOnlyCollection<long> owned)
    {
        if (settings != null)
        {
            if (_settings != settings || !_owned.SetEquals(owned))
            {
                _settings = settings;
                _owned = [.. owned];
                _nextRefreshAt = 0;

                _log.Write(
                    $"Snapshot starting={settings.Starting} max={settings.Maximum} items={string.Join(",", _owned)}"
                );
            }
        }
        if (_settings == null || !manager.InRuneforge())
        {
            Dispose();
            return;
        }
        ApplyStarting(manager);
        if (Environment.TickCount64 < _nextRefreshAt)
            return;
        _nextRefreshAt = Environment.TickCount64 + 1000;
        foreach (var toggler in Resources.FindObjectsOfTypeAll<DifficultyCompletionToggler>())
            if (toggler != null && toggler.gameObject.scene.isLoaded)
                ShowNightmare(toggler);
        foreach (var stone in Resources.FindObjectsOfTypeAll<DifficultyInteractable>())
            if (stone != null && stone.gameObject.scene.isLoaded)
                RefreshGhost(stone);
    }

    private void ApplyStarting(LevelManager manager)
    {
        if (_settings == null || !manager.IsRuneforgeRoom() || manager.tutorialCompleted?.Value != true)
            return;
        var save = manager.saveManager;
        if (!save.HasProfile() || save.economy == null)
            return;

        foreach (var element in save.economy.playerSettings)
        {
            var setting = element.TryCast<SavedInt>();
            var variable = setting?.variable;
            if (!GameDifficulty.IsSettingVariable(variable))
                continue;
            var fresh = _freshProfiles.Contains((save.Pointer, save.ProfileIndex));
            if (!fresh && _settings.Allows(variable.Value, _owned))
                return;

            var previous = variable.Value;
            variable.Value = _settings.Starting;
            save.SetEconomyDirty();
            _freshProfiles.Remove((save.Pointer, save.ProfileIndex));
            _log.Write(
                $"Selected starting profile={save.ProfileIndex} fresh={fresh} old={previous} new={variable.Value}"
            );

            return;
        }
    }
}
