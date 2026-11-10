using System.Reflection;
using System.Text.Json;

namespace UntilYouFallArchipelago;

internal sealed record EnemyCheck(long Id, string Name, string[] Guids, bool ExcludeFromSpawnTrap = false)
{
    public string LocationName => "First Kill: " + Name;
}

internal static class EnemyChecks
{
    public static readonly EnemyCheck[] All = Load();

    public static EnemyCheck? ById(long id) => All.FirstOrDefault(enemy => enemy.Id == id);

    public static EnemyCheck? ByGuid(string? guid) =>
        All.FirstOrDefault(enemy =>
            enemy.Guids.Any(value => string.Equals(value, guid, StringComparison.OrdinalIgnoreCase))
        );

    public static bool IsCombatKill(
        string? campaignGuid,
        int levelIndex,
        bool loading,
        bool playerAlive,
        bool alreadyDying,
        float health
    ) =>
        !loading
        && playerAlive
        && !alreadyDying
        && health <= 0
        && StageChecks.TryGetStage(campaignGuid, levelIndex, true, out _);

    private static EnemyCheck[] Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("UntilYouFall.Enemies.json")!;

        return JsonSerializer.Deserialize<EnemyCheck[]>(stream)!;
    }
}
