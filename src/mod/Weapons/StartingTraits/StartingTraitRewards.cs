using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSG.Claymore.Armaments;
using Il2CppSG.Claymore.Interaction;
using Il2CppSG.Claymore.Interaction.Rewards;
using Il2CppSG.Claymore.Level;
using Il2CppSG.GlobalEvents;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal sealed partial class StartingTraitRewards : IDisposable
{
    private sealed class Group
    {
        public RewardSpawner Spawner { get; }
        public ArmamentAugment[] Rewards { get; }
        public long[] Items { get; }
        public int Loading { get; set; }
        public bool Retired { get; set; }

        public Group(RewardSpawner spawner, ArmamentAugment[] rewards)
        {
            Spawner = spawner;
            Rewards = rewards;
            Items = rewards
                .Select(reward => StartingTraitRules.All.Single(item => item.RewardGuid == reward.GUID).Id)
                .ToArray();
            Loading = rewards.Length;
        }
    }

    private const string CompleteClipName = "sfx_CLAY_rewardseq_end_stereo_01";
    private const string PickClipName = "sfx_CLAY_rewardseq_gemappear_merged";
    private const float SkipCheckInterval = 0.1f;
    private static StartingTraitRewards _instance = null!;
    private readonly ArchipelagoClient _client;
    private readonly LogChannel _log;
    private readonly Dictionary<IntPtr, Group> _groups = [];
    private readonly Dictionary<IntPtr, (RewardInteractable Reward, long Item, Group Group)> _rewards = [];
    private readonly List<GameObject> _roots = [];
    private readonly List<ParticleSystem> _feedback = [];
    private readonly GlobalEvent _localEvent;
    private LevelManager? _manager;
    private RewardSpawner? _source;
    private RewardMomentController? _moment;
    private StartingTraitRun? _run;
    private string? _identity;
    private bool _newRun;
    private bool _pending;
    private bool _presented;
    private bool _assetsRead;
    private bool _appearancePlayed;
    private Vector3? _offerOrigin;
    private Vector3 _offerForward;
    private float? _dismissAt;
    private float _nextSkipCheck;
    private AudioSource? _audio;
    private readonly Dictionary<long, ArmamentAugment> _assets = [];

    public StartingTraitRewards(HarmonyLib.Harmony harmony, ArchipelagoClient client, Action<string> log)
    {
        _instance = this;
        _client = client;
        _log = log.Channel("APStartingTraits");
        _localEvent = ScriptableObject.CreateInstance<GlobalEvent>();
        _localEvent.name = "APStartingTraits.Local";
        harmony.PatchAll(typeof(StartingTraitRewards));
    }

    [HarmonyPatch(typeof(LevelManager), nameof(LevelManager.StartRun))]
    [HarmonyPostfix]
    private static void NewRun(LevelManager __instance) => _instance.Begin(__instance, fresh: true);

    [HarmonyPatch(typeof(LevelManager), nameof(LevelManager.RestoreRun))]
    [HarmonyPostfix]
    private static void Restored(LevelManager __instance) => _instance.Begin(__instance, fresh: false);

    private static string RunKey(LevelManager manager) =>
        $"{manager.saveManager.ProfileIndex}:{manager.Campaign.GUID}:{manager.CurrentSeed}";

    public void StageCleared(LevelManager manager, int stage)
    {
        if (manager.Pointer != _manager?.Pointer || _identity != _client.StartingTraitIdentity)
            return;

        _client.RecordStartingTraitStageClear(RunKey(manager), stage);
    }

    private void Begin(LevelManager manager, bool fresh)
    {
        ClearFeedback();
        Clear();
        _manager = manager;
        _identity = _client.StartingTraitIdentity;
        _newRun = fresh;
        _source = null;
        _moment = null;
        _assetsRead = false;
        _assets.Clear();

        _log.Write($"run {(fresh ? "new" : "restore")} seed={manager.CurrentSeed}");
    }

    [HarmonyPatch(typeof(LevelManager), nameof(LevelManager.EndRun))]
    [HarmonyPrefix]
    private static void Ended()
    {
        _instance.ClearFeedback();
        _instance.Clear();
        _instance._pending = false;
        _instance._newRun = false;
    }

    [HarmonyPatch(typeof(LevelManager), nameof(LevelManager.StartLoad))]
    [HarmonyPrefix]
    private static void Loading()
    {
        _instance.ClearFeedback();
        _instance.Clear();
        _instance._pending = false;
        _instance._presented = false;
        _instance._source = null;
        _instance._moment = null;
    }

    [HarmonyPatch(typeof(RewardMomentController), nameof(RewardMomentController.Start))]
    [HarmonyPostfix]
    private static void FindSource(RewardMomentController __instance)
    {
        var spawner = __instance.rewardSpawner;
        if (spawner != null && !_instance._groups.ContainsKey(spawner.Pointer))
        {
            _instance._source = spawner;
            _instance._moment = __instance;
        }
    }

    [HarmonyPatch(typeof(LevelLoadingPhase), nameof(LevelLoadingPhase.Invoke))]
    [HarmonyPostfix]
    private static void Ready(LevelLoadingPhase __instance)
    {
        var manager = _instance._manager;
        if (
            _instance._presented
            || manager == null
            || manager.OnLevelReady?.Pointer != __instance.Pointer
            || manager.LevelIndex != 0
            || !manager.HasEncounter()
        )
            return;

        _instance._pending = true;
    }

    public void Update()
    {
        for (var index = _feedback.Count - 1; index >= 0; index--)
        {
            var effect = _feedback[index];
            if (effect != null && effect.IsAlive(true))
                continue;
            if (effect != null)
                Object.Destroy(effect.gameObject);
            _feedback.RemoveAt(index);
        }
        if (_identity != _client.StartingTraitIdentity)
        {
            ClearFeedback();
            Clear();
            _identity = _client.StartingTraitIdentity;
            _pending = false;
            _newRun = false;
        }
        CheckSkippedOffer();
        if (_run?.Complete == true)
        {
            if (_dismissAt == null)
                Dismiss();
            if (Time.time >= _dismissAt)
                Clear();
        }
        if (!_pending || _manager == null || _source == null || _manager.IsRoomLoading())
            return;
        var combatant = _source.playerEntity?.Value?.PlayerCombatant;
        if (combatant?.Head == null || combatant.Feet == null)
            return;
        var camera = Camera.main;
        if (camera == null || !camera.isActiveAndEnabled)
            return;

        _pending = false;
        _presented = true;
        try
        {
            Offer(camera.transform);
        }
        catch (Exception exception)
        {
            Clear();
            _log.Write($"offer failed: {exception}");
        }
        _newRun = false;
    }

    private void Offer(Transform view)
    {
        var manager = _manager!;
        if (!_assetsRead)
        {
            _assetsRead = true;
            var byGuid = StartingTraitRules.All.ToDictionary(trait => trait.RewardGuid, trait => trait.Id);
            foreach (var augment in Resources.FindObjectsOfTypeAll<TraitAugment>())
                if (augment != null && byGuid.TryGetValue(augment.GUID, out var item))
                    _assets[item] = augment;
        }
        var left = _source!.leftHandState.PlayerHand;
        var right = _source.rightHandState.PlayerHand;
        var eligible = _assets
            .Where(entry => Usable(entry.Value, left) || Usable(entry.Value, right))
            .Select(entry => entry.Key)
            .ToArray();

        _log.Write(
            $"catalog assets={_assets.Count}/28 eligible={eligible.Length} owned={_client.StartingTraitItemCounts.Count}"
        );
        if (eligible.Length == 0)
            StageRewardDiagnosticGuard.Run(
                () =>
                    _log.Write(
                        $"no eligible traits spawner={_source.GetInstanceID()} scene={_source.gameObject.scene.name} left=[{FormatEligibilityForLog(left)}] right=[{FormatEligibilityForLog(right)}]"
                    ),
                exception => _log.Write($"eligibility diagnostic failed: {exception.Message}")
            );
        var key = RunKey(manager);
        _run = _client.PrepareStartingTraits(key, _newRun, eligible, new System.Random());
        if (_run == null || _run.Complete)
            return;
        var remaining = _run.Remaining;
        if (remaining.Any(item => !_assets.ContainsKey(item)))
            throw new InvalidOperationException("A saved starting reward asset is unavailable; refusing to reroll");

        _log.Write(
            $"offer key={key} items={string.Join(",", remaining)} taken={_run.Taken.Length} limit={_run.PickLimit}"
        );
        var combatant = _source.playerEntity.Value.PlayerCombatant;
        Spawn(remaining.Select(item => _assets[item]).ToArray(), view, combatant.Feet);
    }

    private void Spawn(ArmamentAugment[] rewards, Transform view, Transform feet)
    {
        var pivot = new GameObject("APStartingTraits.Pivot");
        pivot.SetActive(false);
        _roots.Add(pivot);
        pivot.transform.SetPositionAndRotation(_source!.repoPivot.position, _source.repoPivot.rotation);
        var clone = Object.Instantiate(_source.gameObject, pivot.transform, false);
        clone.name = "APStartingTraits.Spawner";
        clone.transform.SetPositionAndRotation(_source.transform.position, _source.transform.rotation);
        var spawner = clone.GetComponent<RewardSpawner>();
        ConfigureSlots(spawner, rewards.Length);
        spawner.repoPivot = pivot.transform;
        spawner.initOnStart = false;
        spawner.isInitialized = false;
        spawner.doRewardRepositioning = false;
        spawner.minDistance = 0f;
        spawner.maxDistance = float.PositiveInfinity;
        var rotation = Quaternion.Euler(0f, view.eulerAngles.y, 0f);
        var position =
            new Vector3(view.position.x, feet.position.y, view.position.z)
            + rotation * Vector3.forward * (_source.distFromPlayer + 1f);
        pivot.transform.SetPositionAndRotation(position, rotation);
        var offset = position - spawner.target.position;
        offset.y = 0f;
        clone.transform.position += offset;
        _groups.Add(spawner.Pointer, new Group(spawner, rewards));
        _offerOrigin = position;
        _offerForward = rotation * Vector3.forward;
        clone.SetActive(true);
        pivot.SetActive(true);
        spawner.HandlePop();

        _log.Write(
            $"placed choices={rewards.Length} view={view.position.ToString("F3")} forward={view.forward.ToString("F3")} feet={feet.position.ToString("F3")} pivot={pivot.transform.position.ToString("F3")} init={spawner.initPos.ToString("F3")} offset={offset.ToString("F3")} distance={spawner.distFromPlayer:F3} travel={spawner.minDistance}/{spawner.maxDistance} follow={spawner.doRewardRepositioning}"
        );
    }

    private void ConfigureSlots(RewardSpawner spawner, int count)
    {
        var center = spawner.spawnPoints[0];
        var template = Object.Instantiate(center.gameObject, spawner.transform, false);
        template.name = "APStartingTraits.SlotTemplate";
        template.SetActive(false);
        foreach (var original in spawner.spawnPoints)
        {
            var line = original.GetComponent<LineRenderer>();
            if (line != null)
                line.enabled = false;
            foreach (var particles in original.GetComponentsInChildren<ParticleSystem>(true))
                particles.gameObject.SetActive(false);
        }
        var points = new Il2CppSystem.Collections.Generic.List<Transform>();
        var lines = new List<LineRenderer>();
        var origins = new List<Transform>();
        var tendrils = spawner.rewardTendrilController;
        for (var index = 0; index < count; index++)
        {
            var point = Object.Instantiate(template, center, false).transform;
            point.name = $"APStartingTraits.Slot{index + 1}";
            var offset = StartingTraitRules.SlotOffset(count, index);
            point.localPosition = new Vector3(offset.X, offset.Y, 0f);
            point.localRotation = Quaternion.identity;
            point.localScale = Vector3.one;
            point.gameObject.SetActive(true);
            points.Add(point);
            lines.Add(point.GetComponent<LineRenderer>());
            if (tendrils != null)
                origins.Add(Object.Instantiate(tendrils.origins[0], spawner.transform, true));

            _log.Write(
                $"slot={index} offset={point.localPosition.ToString("F3")} particles={point.GetComponentsInChildren<ParticleSystem>(true).Length}"
            );
        }
        Object.Destroy(template);
        if (tendrils != null)
        {
            tendrils.tendrils = new Il2CppReferenceArray<LineRenderer>(lines.ToArray());
            tendrils.origins = new Il2CppReferenceArray<Transform>(origins.ToArray());
            tendrils.targets = new Il2CppReferenceArray<Transform>(
                Enumerable.Range(0, count).Select(index => points[index]).ToArray()
            );
        }
        spawner.spawnPoints = points;
    }

    private void CheckSkippedOffer()
    {
        if (_run == null || _run.Complete || _offerOrigin == null || _manager == null || _source == null)
            return;
        if (Time.unscaledTime < _nextSkipCheck)
            return;
        _nextSkipCheck = Time.unscaledTime + SkipCheckInterval;
        if (_manager.IsRoomLoading())
            return;
        var feet = _source.playerEntity?.Value?.PlayerCombatant?.Feet;
        if (feet == null)
            return;
        var distance = Vector3.Dot(feet.position - _offerOrigin.Value, _offerForward);
        if (!StartingTraitRules.ShouldSkip(_run, distance) || !_client.SkipStartingTraits(_run.Key))
            return;
        _run = StartingTraitRules.Skip(_run);

        _log.Write(
            $"skipped remaining offer forwardDistance={distance:F3} origin={_offerOrigin.Value.ToString("F3")} taken={_run.Taken.Length}"
        );
    }

    private void PlayFeedback(bool complete, Transform target)
    {
        StageRewardDiagnosticGuard.Run(
            () => PlaySound(complete),
            exception => _log.Write($"sound failed: {exception}")
        );
        StageRewardDiagnosticGuard.Run(
            () =>
            {
                var count = 0;
                if (complete && _moment?.crushParticlesToPlay != null)
                {
                    foreach (var particles in _moment.crushParticlesToPlay)
                    {
                        if (particles == null)
                            continue;
                        var effect = Object.Instantiate(particles, target.position, target.rotation);
                        _feedback.Add(effect);
                        effect.gameObject.SetActive(true);
                        effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        foreach (var system in effect.GetComponentsInChildren<ParticleSystem>(true))
                        {
                            var main = system.main;
                            main.loop = false;
                        }
                        effect.Play(true);
                        count++;
                    }
                }
                _log.Write($"feedback complete={complete} particles={count}");
            },
            exception => _log.Write($"feedback failed: {exception}")
        );
    }

    private void PlaySound(bool complete)
    {
        var name = complete ? CompleteClipName : PickClipName;
        var clip = SceneCache.Clip(name) ?? SceneCache.Clip(NoticeSound.DefaultClipName);
        if (clip == null)
        {
            _log.Write($"sound not loaded requested={name}");
            return;
        }
        if (_audio == null)
        {
            _audio = new GameObject("APStartingTraits.Sound").AddComponent<AudioSource>();
            _audio.spatialBlend = 0f;
            _audio.playOnAwake = false;
        }
        if (clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();
        _audio.PlayOneShot(clip);

        _log.Write($"sound complete={complete} requested={name} played={clip.name} loadState={clip.loadState}");
    }

    [HarmonyPatch(typeof(RewardSpawner.RewardLoader), nameof(RewardSpawner.RewardLoader.OnResourceLoadComplete))]
    [HarmonyPrefix]
    private static bool AllowLoad(RewardSpawner.RewardLoader __instance, bool __0, ref Object __1)
    {
        if (__instance.Spawner is null || !_instance._groups.TryGetValue(__instance.Spawner.Pointer, out var group))
            return true;
        var pooledReward = __1?.TryCast<RewardInteractable>();
        if (__0 && pooledReward != null)
            pooledReward.gameObject.SetActive(false);
        if (group.Retired)
            return false;
        if (__0 && __1 != null)
        {
            try
            {
                var pooled = __1.GetInstanceID();
                __1 = Object.Instantiate(__1, __instance.Spawner.transform);
                var copy = __1.TryCast<RewardInteractable>();
                if (copy != null && !copy.enabled && copy.Visuals == null && copy.preloadedVisuals != null)
                {
                    copy.visuals = copy.preloadedVisuals;
                    copy.visuals.Initialize();
                    _instance._log.Write(
                        $"prepared disabled crystal slot={__instance.Index} instance={copy.GetInstanceID()} visuals={copy.visuals.GetInstanceID()}"
                    );
                }
                _instance._log.Write(
                    $"isolated crystal slot={__instance.Index} pooled={pooled} pooledActive={pooledReward?.gameObject.activeSelf} instance={__1.GetInstanceID()}"
                );
            }
            catch (Exception exception)
            {
                _instance.Clear();
                _instance._log.Write($"resource copy failed: {exception}");
                return false;
            }
        }

        return true;
    }

    [HarmonyPatch(typeof(RewardSpawner.RewardLoader), nameof(RewardSpawner.RewardLoader.OnResourceLoadComplete))]
    [HarmonyPostfix]
    private static void Loaded(RewardSpawner.RewardLoader __instance)
    {
        if (__instance.Spawner is null || !_instance._groups.TryGetValue(__instance.Spawner.Pointer, out var group))
            return;

        group.Loading--;
        if (group.Loading == 0 && !group.Retired && !_instance._appearancePlayed)
        {
            _instance._appearancePlayed = true;
            _instance.PlayFeedback(false, group.Spawner.target);
        }
        if (group.Retired && group.Loading <= 0)
            _instance._groups.Remove(__instance.Spawner.Pointer);
    }

    private void Dismiss()
    {
        var duration = 0f;
        foreach (var group in _groups.Values)
            group.Retired = true;
        foreach (var entry in _rewards.Values)
        {
            if (entry.Reward == null || _run!.Taken.Contains(entry.Item))
                continue;

            StageRewardDiagnosticGuard.Run(
                () =>
                {
                    var hand = entry.Reward.ActiveHand;
                    if (hand != null && hand.HeldInteractable?.Pointer == entry.Reward.Pointer)
                        hand.ForceSummon();
                    duration = Mathf.Max(duration, entry.Reward.Visuals?._dematerializeAnimationClip?.length ?? 1f);
                    entry.Reward.HandleOtherRewardCrushed();
                },
                exception => _log.Write($"dismiss failed item={entry.Item}: {exception}")
            );
        }
        _dismissAt = Time.time + duration;
        _log.Write($"offer dismissed animationDuration={duration:F3}");
    }

    private void Clear()
    {
        foreach (var entry in _rewards.Values)
        {
            if (entry.Reward == null)
                continue;

            var hand = entry.Reward.ActiveHand;
            if (hand != null && hand.HeldInteractable?.Pointer == entry.Reward.Pointer)
                hand.ForceGrabEnd();
            entry.Reward.gameObject.SetActive(false);
            Object.Destroy(entry.Reward.gameObject);
        }
        _rewards.Clear();
        foreach (var pair in _groups.ToArray())
        {
            pair.Value.Retired = true;
            if (pair.Value.Loading <= 0)
                _groups.Remove(pair.Key);
        }
        foreach (var root in _roots)
            if (root != null)
            {
                root.SetActive(false);
                Object.Destroy(root);
            }
        _roots.Clear();
        _run = null;
        _offerOrigin = null;
        _appearancePlayed = false;
        _dismissAt = null;
    }

    private void ClearFeedback()
    {
        foreach (var effect in _feedback)
            if (effect != null)
                Object.Destroy(effect.gameObject);
        _feedback.Clear();
        if (_audio != null)
            Object.Destroy(_audio.gameObject);
        _audio = null;
    }

    public void Dispose()
    {
        ClearFeedback();
        Clear();
        Object.Destroy(_localEvent);
    }
}
