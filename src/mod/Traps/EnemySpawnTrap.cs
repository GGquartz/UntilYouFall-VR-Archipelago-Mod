using HarmonyLib;
using Il2CppSG.Claymore.Combat.Combatants;
using Il2CppSG.Claymore.HitSystem;
using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.NPCs;
using Il2CppSG.GlobalEvents;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal sealed class EnemySpawnTrap : IDisposable
{
    private const string TemplateName = "AP Enemy Trap Template";
    private const string SpawnName = "AP Enemy Trap Spawn";
    private const float MinimumFacingDistanceMetersSquared = 0.001f;
    private const float MinimumCameraForwardSquared = 0.001f;
    private const float PlayerNavMeshSearchRadiusMeters = 2.5f;
    private const float FallbackAgentRadiusMeters = 0.4f;
    private const float MinimumAgentRadiusMeters = 0.2f;
    private const float FallbackAgentHeightMeters = 1.8f;

    private sealed record Template(
        GameObject Root,
        EnemyTrapCandidate Candidate,
        int SourceInstance,
        Vector3 OriginalPosition,
        Quaternion OriginalRotation
    );

    private static EnemySpawnTrap _instance = null!;
    private readonly Func<LevelManager?> _level;
    private readonly LogChannel _log;
    private readonly Action<ApNotice> _notifyTechnical;
    private readonly HashSet<int> _spawnedEnemies = [];
    private readonly Dictionary<int, Vector3> _pendingSpawnPositions = [];
    private readonly Dictionary<int, Enemy> _spawnSnapshots = [];
    private long _nextSnapshotAt;
    private Template? _template;
    private GameObject? _templateContainer;
    private bool _captureAllowed;
    private bool _capturing;
    private bool _cloneAwakeObserved;
    private long? _lastSpawnRequestAt;
    private bool _roomEnemiesCleared;
    private bool _runEnded;
    private GlobalEvent? _suppressedRoomClearEvent;

    public EnemySpawnTrap(
        HarmonyLib.Harmony harmony,
        Func<LevelManager?> level,
        Action<string> log,
        Action<ApNotice> notifyTechnical
    )
    {
        _instance = this;
        _level = level;
        _log = log.Channel("APEnemyTrapDiag");
        _notifyTechnical = notifyTechnical;
        harmony.PatchAll(typeof(EnemySpawnTrap));
    }

    public void Spawn()
    {
        var now = Environment.TickCount64;
        var burstDuplicate = EnemySpawnTrapRules.IsBurstDuplicate(now, _lastSpawnRequestAt);
        _lastSpawnRequestAt = now;
        if (burstDuplicate)
        {
            _log.Write("Spawn request dropped");
            return;
        }

        var manager = _level();
        if (manager.IsRuneforgeRoom())
        {
            Reject(EnemyTrapSpawnFailure.InRuneforge, "current room is runeforge");
            return;
        }
        if (manager?.IsRoomLoading() == true)
        {
            Reject(
                EnemyTrapSpawnFailure.SceneLoading,
                $"scene is loading loading={manager.IsLoading} coreLoading={manager.IsCoreLoading}"
            );
            return;
        }
        if (!EnemySpawnTrapRules.HasActiveEncounter(manager.HasEncounter(), _runEnded))
        {
            Reject(EnemyTrapSpawnFailure.NoActiveEncounter, _runEnded ? "run has ended" : "no active encounter");
            return;
        }
        if (manager?.CurrentLevel?.Room?.IsBossRoom == true)
        {
            Reject(EnemyTrapSpawnFailure.BossRoom, "current room is a boss room");
            return;
        }
        if (_template == null)
        {
            Reject(EnemyTrapSpawnFailure.NoEnemyTemplate, "no supported enemy template captured in this stage");
            return;
        }

        var camera = ActiveCamera();
        if (camera == null)
        {
            Reject(EnemyTrapSpawnFailure.NoActiveCamera, "no active camera");
            return;
        }
        if (!TryFindPosition(camera, _template, out var position, out var placement))
        {
            Reject(
                EnemyTrapSpawnFailure.NoLocationFound,
                $"no reachable clear navmesh position enemy='{_template.Candidate.Name}'"
            );
            return;
        }

        var beforeEncounter = manager!.CurrentLevel.Encounter.EnemyCount;
        GameObject? spawned = null;
        try
        {
            var towardPlayer = camera.transform.position - position;
            towardPlayer.y = 0f;
            var rotation =
                towardPlayer.sqrMagnitude > MinimumFacingDistanceMetersSquared
                    ? Quaternion.LookRotation(towardPlayer.normalized, Vector3.up)
                    : _template.OriginalRotation;
            spawned = Object.Instantiate(_template.Root, position, rotation);
            spawned.name = SpawnName;
            var enemy =
                spawned.GetComponentInChildren<Enemy>(true)
                ?? throw new InvalidOperationException("Spawned template has no Enemy component");
            _spawnedEnemies.Add(enemy.GetInstanceID());
            _pendingSpawnPositions[enemy.GetInstanceID()] = position;
            spawned.SetActive(true);

            _log.Write(
                $"Spawn requested enemy='{_template.Candidate.Name}' "
                    + $"guid={_template.Candidate.Guid} challenge={_template.Candidate.Challenge} "
                    + $"placement={placement} position={position} "
                    + $"encounter={beforeEncounter}->{manager.CurrentLevel.Encounter.EnemyCount} instance={enemy.GetInstanceID()}"
            );
        }
        catch (Exception exception)
        {
            if (spawned != null)
                Object.Destroy(spawned);
            Reject(EnemyTrapSpawnFailure.InternalError, $"spawn failed: {exception}");
        }
    }

    private void Reject(EnemyTrapSpawnFailure failure, string detail)
    {
        _log.Write($"Spawn rejected: {detail}");
        var message = EnemySpawnTrapRules.FailureMessage(failure);
        _notifyTechnical(ApNotice.Error(message));
    }

    public void Dispose() => ClearTemplate("shutdown");

    public void Update()
    {
        var now = Environment.TickCount64;
        if (now < _nextSnapshotAt || _spawnSnapshots.Count == 0)
            return;

        _nextSnapshotAt = now + 1000;
        foreach (var enemy in _spawnSnapshots.Values.ToArray())
            if (enemy != null)
                LogEnemyState(enemy, "watchdog");
    }

    public void RunEnded()
    {
        if (_runEnded)
            return;
        _runEnded = true;
        _log.Write("Run ended");
    }

    [HarmonyPatch(typeof(LevelManager), nameof(LevelManager.LoadEndRun), typeof(bool))]
    [HarmonyPrefix]
    private static void OnLoadEndRun(LevelManager __instance)
    {
        if (_instance._level()?.Pointer == __instance.Pointer)
            _instance.RunEnded();
    }

    public void RoomEnemiesCleared()
    {
        var manager = _level();
        if (!_captureAllowed || _roomEnemiesCleared || manager == null || manager.IsRoomLoading())
            return;

        _roomEnemiesCleared = true;
        _log.Write("Room enemies cleared");
    }

    [HarmonyPatch(typeof(EnemyManager), nameof(EnemyManager.RemoveItem), typeof(Enemy))]
    [HarmonyPrefix]
    private static void BeginRemoveEnemy(EnemyManager __instance, Enemy item, out GlobalEvent? __state)
    {
        __state = _instance._suppressedRoomClearEvent;
        if (
            EnemySpawnTrapRules.SuppressRepeatedRoomClear(
                _instance._spawnedEnemies.Contains(item.GetInstanceID()),
                _instance._roomEnemiesCleared
            )
        )
            _instance._suppressedRoomClearEvent = __instance.onLastEnemyKilled;
    }

    [HarmonyPatch(typeof(EnemyManager), nameof(EnemyManager.RemoveItem), typeof(Enemy))]
    [HarmonyFinalizer]
    private static Exception? EndRemoveEnemy(Exception? __exception, GlobalEvent? __state)
    {
        _instance._suppressedRoomClearEvent = __state;
        return __exception;
    }

    [HarmonyPatch(typeof(GlobalEvent), nameof(GlobalEvent.Raise), new Type[0])]
    [HarmonyPrefix]
    private static bool AllowRoomClear(GlobalEvent __instance)
    {
        if (_instance._suppressedRoomClearEvent?.Pointer != __instance.Pointer)
            return true;
        _instance._log.Write($"Last enemy event suppressed event='{__instance.name}' roomAlreadyCleared=True");
        return false;
    }

    [HarmonyPatch(typeof(Enemy), nameof(Enemy.Awake))]
    [HarmonyPrefix]
    private static void OnEnemyAwake(Enemy __instance)
    {
        if (_instance._capturing)
        {
            _instance._cloneAwakeObserved = true;
            return;
        }
        if (__instance.name.StartsWith(SpawnName, StringComparison.Ordinal))
        {
            _instance._log.Write(
                $"Spawn awake instance={__instance.GetInstanceID()} definition={__instance.EntityDefinition?.TryCast<NPCCombatantDefinition>()?.GUID}"
            );
            return;
        }
        try
        {
            _instance.TryCaptureBeforeAwake(__instance);
        }
        catch (Exception exception)
        {
            _instance._log.Write($"Pre awake capture failed: {exception}");
        }
    }

    [HarmonyPatch(typeof(Enemy), nameof(Enemy.FinishInitialization))]
    [HarmonyPostfix]
    private static void OnEnemyReady(Enemy __instance)
    {
        if (!__instance.name.StartsWith(SpawnName, StringComparison.Ordinal))
            return;

        var instance = __instance.GetInstanceID();
        _instance._spawnedEnemies.Add(instance);
        _instance._spawnSnapshots[instance] = __instance;
        var agent = __instance.GetComponentInChildren<NavMeshAgent>(true);
        var warped =
            _instance._pendingSpawnPositions.TryGetValue(instance, out var position)
            && agent != null
            && agent.enabled
            && agent.Warp(position);
        var encounter = _instance._level()?.CurrentLevel?.Encounter;
        _instance._log.Write(
            $"Spawn ready instance={instance} warped={warped} manager={ManagerCount(__instance.EnemyManager)} encounter={encounter?.EnemyCount ?? -1}"
        );
        _instance.LogEnemyState(__instance, "ready");
    }

    [HarmonyPatch(typeof(EnemyKillController), nameof(EnemyKillController.Kill))]
    [HarmonyPrefix]
    private static void OnEnemyKill(EnemyKillController __instance)
    {
        try
        {
            var enemy = __instance.Enemy;
            if (
                enemy != null
                && (
                    _instance._spawnedEnemies.Contains(enemy.GetInstanceID())
                    || _instance._template?.SourceInstance == enemy.GetInstanceID()
                )
            )
                _instance.LogEnemyState(enemy, "kill-request");
        }
        catch (Exception exception)
        {
            _instance._log.Write($"Kill snapshot failed: {exception.Message}");
        }
    }

    private void LogEnemyState(Enemy enemy, string phase)
    {
        try
        {
            var visuals = enemy.Visuals;
            var renderers =
                visuals != null
                    ? visuals.GetComponentsInChildren<Renderer>(true)
                    : enemy.GetComponentsInChildren<Renderer>(true);
            var visibleRenderers = 0;
            foreach (var renderer in renderers)
                if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                    visibleRenderers++;

            _log.Write(
                $"Enemy snapshot phase={phase} instance={enemy.GetInstanceID()} "
                    + $"source={_template?.SourceInstance} health={enemy.EnemyHealth?.CurrentHealth} "
                    + $"dying={enemy.KillController?._isDying} active={enemy.gameObject.activeInHierarchy} "
                    + $"position={enemy.transform.position} scale={enemy.transform.lossyScale} "
                    + $"visuals={visuals?.GetInstanceID()} visualsActive={visuals?.gameObject.activeInHierarchy} "
                    + $"renderers={visibleRenderers}/{renderers.Length} visualsPosition={visuals?.transform.position} visualsScale={visuals?.transform.lossyScale}"
            );
        }
        catch (Exception exception)
        {
            _log.Write($"Enemy snapshot failed phase={phase} error={exception.Message}");
        }
    }

    [HarmonyPatch(typeof(Enemy), nameof(Enemy.OnDisable))]
    [HarmonyPostfix]
    private static void OnEnemyDisabled(Enemy __instance)
    {
        if (!_instance._spawnedEnemies.Remove(__instance.GetInstanceID()))
            return;

        _instance._spawnSnapshots.Remove(__instance.GetInstanceID());
        _instance.LogEnemyState(__instance, "disabled");
        _instance._pendingSpawnPositions.Remove(__instance.GetInstanceID());
        _instance._log.Write(
            $"Spawn disabled instance={__instance.GetInstanceID()} manager={ManagerCount(__instance.EnemyManager)}"
        );
    }

    [HarmonyPatch(typeof(LevelManager), nameof(LevelManager.StartLoad))]
    [HarmonyPrefix]
    private static void OnRoomLoad(Campaign.RoomSelection level)
    {
        _instance._runEnded = false;
        _instance._roomEnemiesCleared = false;
        _instance.ClearTemplate("room-load");
        _instance._captureAllowed = EnemySpawnTrapRules.CanCaptureRoom(
            level.Encounter != null,
            level.Room?.IsBossRoom == true
        );
        _instance._log.Write(
            $"Pre awake capture armed={_instance._captureAllowed} room={level.Room?.GUID} encounter={level.Encounter?.GUID} boss={level.Room?.IsBossRoom}"
        );
    }

    private void TryCaptureBeforeAwake(Enemy enemy)
    {
        if (!_captureAllowed)
            return;
        var definition = enemy.EntityDefinition?.TryCast<NPCCombatantDefinition>();
        var check = EnemyChecks.ByGuid(definition?.GUID);
        if (definition == null || check == null || EnemySpawnTrapRules.IsExcludedEnemy(check.Id))
            return;

        var candidate = new EnemyTrapCandidate(definition.GUID, check.Name, definition.ChallengeRating, false);
        if (_template != null)
        {
            if (string.Equals(_template.Candidate.Guid, candidate.Guid, StringComparison.OrdinalIgnoreCase))
                return;
            if (EnemySpawnTrapRules.Strongest(new[] { _template.Candidate, candidate })?.Guid != candidate.Guid)
                return;
        }

        var container = new GameObject(TemplateName + " Container");
        container.SetActive(false);
        GameObject? clone = null;
        try
        {
            _capturing = true;
            _cloneAwakeObserved = false;
            clone = Object.Instantiate(enemy.gameObject, container.transform, true);
            clone.name = TemplateName;
            clone.SetActive(false);
        }
        catch
        {
            Object.Destroy(container);
            throw;
        }
        finally
        {
            _capturing = false;
        }

        if (clone.GetComponentInChildren<Enemy>(true) == null || _cloneAwakeObserved)
        {
            Object.Destroy(container);

            _log.Write(
                $"Pre-Awake template rejected; enemy='{candidate.Name}' "
                    + $"enemyComponent={clone.GetComponentInChildren<Enemy>(true) != null} "
                    + $"cloneAwakeObserved={_cloneAwakeObserved}"
            );
            return;
        }

        ClearTemplate("stronger-template");
        _templateContainer = container;
        _template = new Template(
            clone,
            candidate,
            enemy.GetInstanceID(),
            enemy.transform.position,
            enemy.transform.rotation
        );
        var components = clone.GetComponentsInChildren<Component>(true).Length;
        _log.Write(
            $"Pre awake template captured enemy='{candidate.Name}' guid={candidate.Guid} "
                + $"challenge={candidate.Challenge} components={components} "
                + $"cloneActiveInHierarchy={clone.activeInHierarchy} position={enemy.transform.position}"
        );
    }

    private bool TryFindPosition(Camera camera, Template template, out Vector3 position, out string placement)
    {
        position = default;
        placement = "none";
        var forward = camera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < MinimumCameraForwardSquared)
            forward = Vector3.forward;
        forward.Normalize();

        var areaMask = NavMesh.AllAreas;
        var templateAgent = template.Root.GetComponentInChildren<NavMeshAgent>(true);
        if (templateAgent != null)
            areaMask = templateAgent.areaMask;
        if (
            !NavMesh.SamplePosition(
                camera.transform.position,
                out var playerHit,
                PlayerNavMeshSearchRadiusMeters,
                areaMask
            )
        )
        {
            _log.Write(
                $"Placement rejected point=player reason=no-navmesh camera={camera.transform.position} areaMask={areaMask}"
            );
            return false;
        }

        foreach (var offset in EnemySpawnTrapRules.PlacementOffsets)
        {
            var direction = Quaternion.AngleAxis(offset.AngleDegrees, Vector3.up) * forward;
            var raw = playerHit.position + direction * offset.Distance;
            if (!TryAcceptPosition(raw, playerHit.position, template, areaMask, out position, out var reason))
            {
                _log.Write(
                    $"Placement rejected point=relative:{offset.Distance:F1}m/ {offset.AngleDegrees:F0}deg raw={raw} reason={reason}"
                );
                continue;
            }
            placement = $"relative:{offset.Distance:F1}m/{offset.AngleDegrees:F0}deg";
            return true;
        }

        if (
            TryAcceptPosition(
                template.OriginalPosition,
                playerHit.position,
                template,
                areaMask,
                out position,
                out var originalReason
            )
        )
        {
            placement = "original-spawn";
            return true;
        }
        _log.Write($"Placement rejected point=original-spawn raw={template.OriginalPosition} reason={originalReason}");
        return false;
    }

    private static bool TryAcceptPosition(
        Vector3 raw,
        Vector3 playerPosition,
        Template template,
        int areaMask,
        out Vector3 position,
        out string reason
    )
    {
        position = default;
        reason = "accepted";
        if (!NavMesh.SamplePosition(raw, out var hit, EnemySpawnTrapRules.NavMeshSampleRadius, areaMask))
        {
            reason = $"no-navmesh radius={EnemySpawnTrapRules.NavMeshSampleRadius:F1} areaMask={areaMask}";
            return false;
        }

        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(hit.position, playerPosition, areaMask, path))
        {
            reason = $"path-calculation-failed hit={hit.position} player={playerPosition}";
            return false;
        }
        if (path.status != NavMeshPathStatus.PathComplete)
        {
            reason = $"path-status={path.status} hit={hit.position} player={playerPosition}";
            return false;
        }

        var agent = template.Root.GetComponentInChildren<NavMeshAgent>(true);
        var radius =
            Math.Max(agent?.radius ?? FallbackAgentRadiusMeters, MinimumAgentRadiusMeters)
            + EnemySpawnTrapRules.ClearancePadding;
        var height = Math.Max(agent?.height ?? FallbackAgentHeightMeters, radius * 2f);
        var bottom = hit.position;
        bottom.y = EnemySpawnTrapRules.CapsuleBottomCenterY(hit.position.y, radius);
        var top = bottom + Vector3.up * Math.Max(0f, height - radius * 2f);
        var blockers = Physics.OverlapCapsule(bottom, top, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        if (blockers.Length > 0)
        {
            var blockerNames = string.Join(
                ", ",
                blockers
                    .Take(6)
                    .Select(blocker =>
                        $"'{blocker.name}' layer={blocker.gameObject.layer}:{LayerMask.LayerToName(blocker.gameObject.layer)}"
                    )
            );
            reason =
                $"capsule blocked hit={hit.position} radius={radius:F2} height={height:F2} blockers={blockers.Length} [{blockerNames}]";
            return false;
        }

        position = hit.position;
        return true;
    }

    private void ClearTemplate(string reason)
    {
        if (_templateContainer != null)
            Object.Destroy(_templateContainer);
        if (_template != null)
            _log.Write($"Template cleared reason={reason} enemy='{_template.Candidate.Name}'");
        _template = null;
        _templateContainer = null;
        _spawnedEnemies.Clear();
        _spawnSnapshots.Clear();
        _pendingSpawnPositions.Clear();
    }

    private static Camera? ActiveCamera()
    {
        var camera = Camera.main;
        if (camera == null || !camera.isActiveAndEnabled)
            camera = Object
                .FindObjectsOfType<Camera>()
                .FirstOrDefault(candidate => candidate.isActiveAndEnabled && candidate.stereoEnabled);

        return camera;
    }

    private static int ManagerCount(EnemyManager? manager)
    {
        try
        {
            return manager?.Items?.Count ?? -1;
        }
        catch
        {
            return -1;
        }
    }
}
