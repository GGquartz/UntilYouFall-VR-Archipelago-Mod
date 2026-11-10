namespace UntilYouFallArchipelago;

internal sealed record EnemyTrapCandidate(string Guid, string Name, int Challenge, bool Excluded);

internal readonly record struct EnemyTrapOffset(float Distance, float AngleDegrees);

internal enum EnemyTrapSpawnFailure
{
    InRuneforge,
    SceneLoading,
    NoActiveEncounter,
    BossRoom,
    NoEnemyTemplate,
    NoActiveCamera,
    NoLocationFound,
    InternalError,
}

internal static class EnemySpawnTrapRules
{
    public const long SpawnBurstWindowMs = 1000;
    public const float NavMeshSampleRadius = 1.25f;
    public const float ClearancePadding = 0.2f;
    public const float GroundClearance = 0.05f;

    public static readonly EnemyTrapOffset[] PlacementOffsets = new EnemyTrapOffset[]
    {
        new(3f, 0f),
        new(3f, 25f),
        new(3f, -25f),
        new(3f, 50f),
        new(3f, -50f),
        new(4f, 0f),
        new(4f, 25f),
        new(4f, -25f),
        new(4f, 50f),
        new(4f, -50f),
        new(5f, 0f),
        new(5f, 25f),
        new(5f, -25f),
        new(5f, 50f),
        new(5f, -50f),
    }
        .Concat(
            new[] { 2.5f, 3.5f, 4.5f, 5.5f }.SelectMany(distance =>
                new[] { 75f, -75f, 105f, -105f, 135f, -135f, 180f }.Select(angle => new EnemyTrapOffset(
                    distance,
                    angle
                ))
            )
        )
        .ToArray();

    public static bool IsExcludedEnemy(long checkId) => EnemyChecks.ById(checkId)?.ExcludeFromSpawnTrap == true;

    public static bool CanCaptureRoom(bool hasEncounter, bool isBossRoom) => hasEncounter && !isBossRoom;

    public static bool HasActiveEncounter(bool hasEncounter, bool runEnded) => hasEncounter && !runEnded;

    public static bool IsBurstDuplicate(long now, long? previousRequestAt) =>
        previousRequestAt.HasValue && now - previousRequestAt.Value < SpawnBurstWindowMs;

    public static bool SuppressRepeatedRoomClear(bool isTrap, bool roomEnemiesCleared) => isTrap && roomEnemiesCleared;

    public static string FailureMessage(EnemyTrapSpawnFailure failure) =>
        failure switch
        {
            EnemyTrapSpawnFailure.InRuneforge => "Spawn Failed (In Runeforge)",
            EnemyTrapSpawnFailure.SceneLoading => "Spawn Failed (Scene Loading)",
            EnemyTrapSpawnFailure.NoActiveEncounter => "Spawn Failed (No Active Encounter)",
            EnemyTrapSpawnFailure.BossRoom => "Spawn Failed (Boss Room)",
            EnemyTrapSpawnFailure.NoEnemyTemplate => "Spawn Failed (No Enemy Template)",
            EnemyTrapSpawnFailure.NoActiveCamera => "Spawn Failed (No Active Camera)",
            EnemyTrapSpawnFailure.NoLocationFound => "Spawn Failed (No Location Found)",
            _ => "Spawn Failed (Unknown Error)",
        };

    public static EnemyTrapCandidate? Strongest(IEnumerable<EnemyTrapCandidate> candidates) =>
        candidates
            .Where(candidate => !candidate.Excluded)
            .OrderByDescending(candidate => candidate.Challenge)
            .ThenBy(candidate => candidate.Guid, StringComparer.Ordinal)
            .FirstOrDefault();

    public static float CapsuleBottomCenterY(float groundY, float radius) => groundY + radius + GroundClearance;
}
