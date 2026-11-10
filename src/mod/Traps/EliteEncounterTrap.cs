using HarmonyLib;
using Il2CppSG.Claymore.Interaction;
using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.NPCs;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal sealed class EliteEncounterTrap : IDisposable
{
    private static EliteEncounterTrap _instance = null!;
    private readonly ArchipelagoClient _client;
    private readonly EliteEncounterOverrides? _overrides;
    private readonly Dictionary<EliteEncounterRoom, Room> _clones = [];
    private readonly LogChannel _log;
    private LevelManager? _manager;
    private Room? _roomClone;
    private int _originalRewardId;
    private int? _awaitedRoomId;
    private string? _awaitedReceipt;

    public EliteEncounterTrap(
        HarmonyLib.Harmony harmony,
        ArchipelagoClient client,
        string overridePath,
        Action<string> log
    )
    {
        _instance = this;
        _client = client;
        _log = log.Channel("APEliteTrapDiag");
        try
        {
            _overrides = EliteEncounterOverrides.Open(overridePath);
        }
        catch (Exception exception)
        {
            _log.Write($"Disabled: cannot read saved room overrides {exception}");
        }
        harmony.Patch(
            AccessTools.Method(typeof(LevelManager), nameof(LevelManager.StartLoad)),
            prefix: new HarmonyMethod(typeof(EliteEncounterTrap), nameof(BeforeLoad)) { priority = Priority.First },
            postfix: new HarmonyMethod(typeof(EliteEncounterTrap), nameof(AfterLoad))
        );
        harmony.Patch(
            AccessTools.Method(typeof(RewardSpawner), nameof(RewardSpawner.PopulateSpawnRewards)),
            prefix: new HarmonyMethod(typeof(EliteEncounterTrap), nameof(BeforeRewards)) { priority = Priority.First }
        );
        harmony.Patch(
            AccessTools.Method(typeof(Enemy), nameof(Enemy.FinishInitialization)),
            postfix: new HarmonyMethod(typeof(EliteEncounterTrap), nameof(EnemyReady))
        );
    }

    private static void BeforeLoad(LevelManager __instance, ref Campaign.RoomSelection level)
    {
        try
        {
            _instance.ReplaceNextRoom(__instance, ref level);
        }
        catch (Exception exception)
        {
            _instance._log.Write($"Override failed {exception}");
        }
    }

    private void ReplaceNextRoom(LevelManager manager, ref Campaign.RoomSelection level)
    {
        _awaitedRoomId = null;
        _awaitedReceipt = null;
        if (_overrides == null)
            return;
        _manager = manager;
        var campaign = manager.Campaign;
        if (
            campaign == null
            || !EliteEncounterTrapRules.ShouldReplace(
                true,
                campaign.GUID,
                manager.LevelIndex,
                level.Encounter != null,
                level.Room?.IsBossRoom ?? false,
                manager.IsRoomLoading()
            )
        )
        {
            _log.Write(
                $"Selection unchanged seed={manager.CurrentSeed} index={manager.LevelIndex} campaign={manager.Campaign?.GUID} room={level.Room?.GUID} boss={level.Room?.IsBossRoom} loading={manager.IsLoading}/{manager.IsCoreLoading}"
            );
            return;
        }
        var originalRoom = level.Room;
        if (!manager.saveManager.HasProfile() || originalRoom == null)
            return;

        var profile = manager.saveManager.ProfileIndex;

        var roomKey = new EliteEncounterRoom(
            profile,
            manager.CurrentSeed,
            manager.LevelIndex,
            campaign.GUID,
            originalRoom.GUID
        );
        var restoring = _overrides.TryGetReceipt(roomKey, out var receipt);
        if (!restoring)
        {
            receipt = _client.PendingEliteEncounterTrap;
            if (receipt == null)
            {
                _log.Write(
                    $"Selection unchanged seed={manager.CurrentSeed} index={manager.LevelIndex} room={originalRoom.GUID} reason=no-pending-item"
                );
                return;
            }
            if (_overrides.HasReceipt(receipt))
            {
                _client.CompleteEliteEncounterTrap(receipt);
                _log.Write($"Previously reserved receipt not applied to a second room receipt={receipt}");
                return;
            }
        }
        string eliteRoomGuid;
        if (restoring)
            eliteRoomGuid = _overrides.GetEliteRoomGuid(roomKey);
        else
        {
            var candidates = EliteEncounterTrapRules
                .EliteEncounters.Where(pair => EliteEncounterTrapRules.CanSelectRoom(manager.LevelIndex, pair.RoomGuid))
                .ToArray();
            eliteRoomGuid = candidates[System.Random.Shared.Next(candidates.Length)].RoomGuid;
        }
        var originalRewards = originalRoom?.RewardSet;
        Room? elite = null;
        var rooms = campaign?.allPotentialRooms;
        if (rooms != null)
        {
            for (var index = 0; index < rooms.Count; index++)
            {
                var candidate = rooms[index];
                if (candidate != null && candidate.GUID == eliteRoomGuid)
                {
                    elite = candidate;
                    break;
                }
            }
        }
        if (
            originalRoom == null
            || originalRewards == null
            || elite == null
            || elite.IsBossRoom
            || elite.potentialEncounters == null
            || elite.potentialEncounters.Count != 1
            || !EliteEncounterTrapRules.EliteEncounters.Any(pair =>
                pair.RoomGuid == elite.GUID && pair.EncounterGuid == elite.potentialEncounters[0]?.GUID
            )
        )
        {
            _log.Write(
                $"Override rejected missing original reward pool or exact vanilla elite pair selectedRoom={eliteRoomGuid}"
            );
            return;
        }

        if (!_clones.TryGetValue(roomKey, out var clone) || clone == null || clone.GUID != eliteRoomGuid)
        {
            if (clone != null)
                Object.Destroy(clone);
            clone = Object.Instantiate(elite);
            clone.hideFlags = HideFlags.DontUnloadUnusedAsset;
            _clones[roomKey] = clone;
        }
        clone.rewardSet = originalRewards;
        var replacement = new Campaign.RoomSelection
        {
            Room = clone,
            Encounter = elite.potentialEncounters[0],
            RoomQuality = level.RoomQuality,
        };
        if (!restoring)
            _overrides.Reserve(roomKey, receipt!, eliteRoomGuid);
        _roomClone = clone;
        _originalRewardId = originalRewards.GetInstanceID();
        _awaitedRoomId = clone.GetInstanceID();
        _awaitedReceipt = receipt;

        _log.Write(
            $"Override requested restoring={restoring} receipt={receipt} profile={profile} seed={manager.CurrentSeed} index={manager.LevelIndex} originalRoom={originalRoom.GUID} originalEncounter={level.Encounter?.GUID} room={clone.GUID} encounter={elite.potentialEncounters[0].GUID} quality={level.RoomQuality}/{replacement.RoomQuality} originalReward={originalRewards.name}#{_originalRewardId} replacementReward={clone.RewardSet.name}#{clone.RewardSet.GetInstanceID()} vanillaEliteReward={elite.RewardSet?.name} generatedList=unchanged"
        );
        level = replacement;
    }

    private static void AfterLoad(LevelManager __instance)
    {
        try
        {
            _instance.ConfirmStarted(__instance);
        }
        catch (Exception exception)
        {
            _instance._log.Write($"Load start observation failed {exception}");
        }
    }

    private void ConfirmStarted(LevelManager manager)
    {
        if (_awaitedReceipt == null || _awaitedRoomId == null || _manager?.Pointer != manager.Pointer)
            return;
        if (!manager.IsRoomLoading() && manager.CurrentLevel?.Room?.GetInstanceID() != _awaitedRoomId.Value)
            return;

        if (_client.PendingEliteEncounterTrap == _awaitedReceipt && _client.CompleteEliteEncounterTrap(_awaitedReceipt))
            _awaitedReceipt = null;
    }

    public void Update()
    {
        if (!_awaitedRoomId.HasValue || _manager == null)
            return;

        try
        {
            ConfirmStarted(_manager);
            if (_manager.IsRoomLoading() || _manager.CurrentLevel?.Room?.GetInstanceID() != _awaitedRoomId.Value)
                return;

            var scenes = Enumerable
                .Range(0, SceneManager.sceneCount)
                .Select(index => SceneManager.GetSceneAt(index).name);

            _log.Write(
                $"Load ready seed={_manager.CurrentSeed} index={_manager.LevelIndex} room={_manager.CurrentLevel.Room.GUID} encounter={_manager.CurrentLevel.Encounter?.GUID} scenes={string.Join(",", scenes)}"
            );
            _awaitedRoomId = null;
        }
        catch (Exception exception)
        {
            _awaitedRoomId = null;
            _log.Write($"Load observation failed {exception}");
        }
    }

    private bool IsReplacedRoom() =>
        _manager != null
        && _roomClone != null
        && _manager.CurrentLevel?.Room?.GetInstanceID() == _roomClone.GetInstanceID();

    private static void BeforeRewards(RewardSpawner __instance)
    {
        try
        {
            var manager = __instance.levelManager;
            if (!_instance.IsReplacedRoom() || manager == null || manager.Pointer != _instance._manager?.Pointer)
                return;
            var rewards = manager.CurrentLevel?.Room?.RewardSet;

            _instance._log.Write(
                $"Reward pool read pool={rewards?.name}#{rewards?.GetInstanceID()} expectedId={_instance._originalRewardId} preserved={rewards != null && rewards.GetInstanceID() == _instance._originalRewardId}"
            );
        }
        catch (Exception exception)
        {
            _instance._log.Write($"Reward observation failed {exception}");
        }
    }

    private static void EnemyReady(Enemy __instance)
    {
        try
        {
            if (_instance.IsReplacedRoom())
                _instance._log.Write(
                    $"Enemy ready enemy={__instance.name} enemies={__instance.EnemyManager?.Items?.Count}"
                );
        }
        catch (Exception exception)
        {
            _instance._log.Write($"Enemy observation failed {exception}");
        }
    }

    public void Dispose()
    {
        foreach (var clone in _clones.Values)
            if (clone != null)
                Object.Destroy(clone);
        _clones.Clear();
        _roomClone = null;
    }
}
