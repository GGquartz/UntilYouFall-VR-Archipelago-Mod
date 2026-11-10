using Il2CppSG.Claymore.SaveSystem;

namespace Il2CppSG.Claymore.Level;

public class LevelManager
{
    public bool IsLoading;
    public bool IsCoreLoading;
    public Campaign? Campaign;
    public int LevelIndex;
    public int CurrentSeed;
    public Level? CurrentLevel;
    public Room? titleRoom;
    public ArmamentInventory? armamentInventory;
}

public class Campaign
{
    public string GUID = "";
}

public class Level
{
    public Room? Room;
    public Encounter? Encounter;
}

public class Encounter
{
    public string? GUID;
}

public class Room
{
    public IntPtr Pointer;
    public string? GUID;
    public string? DeveloperName;
    public bool IsBossRoom;
}
