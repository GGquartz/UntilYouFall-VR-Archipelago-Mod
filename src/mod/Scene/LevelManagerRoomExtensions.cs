using System.Diagnostics.CodeAnalysis;
using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.SaveSystem;

namespace UntilYouFallArchipelago;

internal static class LevelManagerRoomExtensions
{
    public static string DescribeForLog(this LevelManager manager)
    {
        var level = manager.CurrentLevel;
        return $"seed={manager.CurrentSeed} index={manager.LevelIndex} campaign={manager.Campaign?.GUID} room={level?.Room?.DeveloperName} roomGuid={level?.Room?.GUID} encounterGuid={level?.Encounter?.GUID} boss={level?.Room?.IsBossRoom}";
    }

    public static bool IsRoomLoading(this LevelManager manager) => manager.IsLoading || manager.IsCoreLoading;

    public static Room? GameplayRoom(this LevelManager manager)
    {
        var room = manager.CurrentLevel?.Room;
        if (manager.IsRoomLoading() || room == null)
            return null;

        return manager.titleRoom != null && room.Pointer == manager.titleRoom.Pointer ? null : room;
    }

    public static bool IsGameplayRoom(this LevelManager manager) => manager.GameplayRoom() != null;

    public static bool HasEncounter([NotNullWhen(true)] this LevelManager? manager) =>
        manager?.CurrentLevel?.Encounter != null;

    public static bool HasProfile([NotNullWhen(true)] this SaveManager? save) => save != null && save.ProfileIndex >= 0;

    public static bool HasProfile([NotNullWhen(true)] this ArmamentInventory? inventory) =>
        inventory != null && inventory.saveManager.HasProfile();

    public static bool IsRuneforgeRoom([NotNullWhen(true)] this LevelManager? manager) =>
        manager?.CurrentLevel?.Room?.GUID == RuneforgeShopRules.RuneforgeRoomGuid;

    public static bool InRuneforge(this LevelManager? manager, bool ignoreLoading = false) =>
        manager != null
        && RuneforgeShopRules.InRuneforge(
            manager.CurrentLevel?.Room?.GUID,
            manager.IsLoading && !ignoreLoading,
            manager.IsCoreLoading
        );

    public static bool InCombatRoom(this LevelManager? manager) =>
        manager != null && !manager.IsRoomLoading() && manager.HasEncounter() && !manager.IsRuneforgeRoom();
}
