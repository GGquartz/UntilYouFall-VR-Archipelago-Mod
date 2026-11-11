using System.ComponentModel.Design;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UntilYouFallArchipelago;

internal sealed class QuestNeed
{
    public string[] Weapons { get; set; } = Array.Empty<string>();
    public string[] AnyWeapons { get; set; } = Array.Empty<string>();
    public string[] Abilities { get; set; } = Array.Empty<string>();
    public int AnyCount { get; set; }
    public bool AnySuper { get; set; }
}

internal sealed class Quest
{
    public string Name { get; set; } = "";
    public int Tier { get; set; }
    public QuestSource Source { get; set; }
    public string Icon { get; set; } = "";
    public string Description { get; set; } = "";
    public int Target { get; set; }
    public int Stage { get; set; }
    public QuestNeed Need { get; set; } = new();
}

internal enum QuestSource
{
    None,
    WeaponMaxed,
    WeaponUpgraded,
    RagdollDistance,
    MeleeBlocks,
    RangedBlocks,
    Dodges,
    GuardBreaks,
    HusksKilled,
    KnightsKilled,
    CaptainsKilled,
    SpellweaversKilled,
    EmpoweredsKilled,
    WeaponsAcquired,
    AetherRun,
    AetherTotal,
    Speedrun,
    NoVitality,
    NoAetherReward,
    HorrorStages,
    EnemiesKilled,
    NoResolve,
    TraitsOnWeapon,
    TraitsEquipped,
    SuperLevel4,
    LongCombo,
    ViciousKills,
    ShardstormKills,
    AllSupers,
    BulwarkBlocks,
    SoulBurnKills,
    Untouched,
    BrinkOfRuin,
    ShopPurchase,
    KillsInWindow,
    MultiHitSwings,
}

internal static class QuestChecks
{
    private const long BaseId = LocationDefinitions.QuestBaseId;
    public static readonly Quest[] All = Load();

    public const int DefaultHintCost = OptionRanges.QuestHintCostDefault;

    public static int ReadHintCost(IDictionary<string, object> slotData)
    {
        if (!slotData.TryGetValue("quest_hint_cost", out var raw))
            return DefaultHintCost;
        if (raw == null || raw is bool)
            throw new InvalidDataException("Invalid quest hint cost in AP slot data");

        var cost = Convert.ToDecimal(raw);
        if (
            cost < OptionRanges.QuestHintCostMinimum
            || cost > OptionRanges.QuestHintCostMaximum
            || cost != decimal.Truncate(cost)
        )
            throw new InvalidDataException(
                $"Quest hint cost must be a number from {OptionRanges.QuestHintCostMinimum} to {OptionRanges.QuestHintCostMaximum}"
            );

        return (int)cost;
    }

    public static string HintCostText(int cost) => cost == 0 ? "Hint Cost: Free" : $"Hint Cost: {cost}";

    public static bool TryGetIndex(long locationId, out int index)
    {
        index = (int)((locationId - BaseId - 1) / LocationDefinitions.QuestRewardStride);

        return locationId > BaseId
            && index >= 0
            && index < All.Length
            && locationId <= LocationId(index, All[index].Tier);
    }

    public static long LocationId(int index, int reward) =>
        BaseId + index * LocationDefinitions.QuestRewardStride + reward;

    public static string Name(int index, int reward) => $"Quest: {All[index].Name} - Reward {reward}";

    public static IEnumerable<long> Ids(IEnumerable<int> active) =>
        active.SelectMany(index => Enumerable.Range(1, All[index].Tier).Select(reward => LocationId(index, reward)));

    public static int[] ReadActive(IDictionary<string, object> slotData)
    {
        if (!slotData.TryGetValue("quests", out var raw) || raw is not System.Collections.IEnumerable list)
            throw new InvalidDataException("Missing quest board list in AP slot data");

        var active = list.Cast<object>().Select(Convert.ToInt32).ToArray();
        if (active.Any(index => index < 0 || index >= All.Length) || active.Distinct().Count() != active.Length)
            throw new InvalidDataException("Invalid quest board list in AP slot data");

        return active;
    }

    public static int RecordKillInWindow(Queue<float> recentKills, float nowSeconds, float windowSeconds)
    {
        recentKills.Enqueue(nowSeconds);
        while (recentKills.Count > 0 && nowSeconds - recentKills.Peek() > windowSeconds)
            recentKills.Dequeue();

        return recentKills.Count;
    }

    public static bool IsClearQuest(Quest quest) =>
        quest.Target == 1
        && quest.Source != QuestSource.RagdollDistance
        && (quest.Need.Weapons.Length > 0 || quest.Need.AnyWeapons.Length > 0);

    public static bool UnlocksOwned(
        Quest quest,
        IReadOnlyCollection<long> weaponIds,
        IReadOnlyCollection<long> abilityIds,
        bool separateSupers
    )
    {
        bool Owns(string name) => WeaponCatalog.ByName(name) is { } weapon && weaponIds.Contains(weapon.Id);

        var need = quest.Need;

        return need.Weapons.All(Owns)
            && (need.AnyWeapons.Length == 0 || need.AnyWeapons.Count(Owns) >= Math.Max(1, need.AnyCount))
            && (
                !separateSupers
                || need.Abilities.All(name =>
                    WeaponAbilityCatalog.ItemIds.TryGetValue(name, out var id) && abilityIds.Contains(id)
                )
            )
            && (
                !separateSupers
                || !need.AnySuper
                || WeaponCatalog.All.Any(weapon =>
                    weaponIds.Contains(weapon.Id) && abilityIds.Contains(weapon.SuperItemId)
                )
            );
    }

    public static bool ClearMatches(Quest quest, int stage, IReadOnlyCollection<string> weaponNames) =>
        stage >= (quest.Stage > 0 ? quest.Stage : StageChecks.Count)
        && quest.Need.Weapons.All(weaponNames.Contains)
        && (
            quest.Need.AnyWeapons.Length == 0
            || quest.Need.AnyWeapons.Count(weaponNames.Contains) >= quest.Need.AnyCount
        );

    public static IEnumerable<QuestSource> KillQuestSources(string enemyName)
    {
        yield return QuestSource.EnemiesKilled;
        if (enemyName == "Aether Horror")
            yield return QuestSource.HorrorStages;
        if (enemyName.Contains("Empowered"))
            yield return QuestSource.EmpoweredsKilled;
        if (enemyName.EndsWith("Husk"))
            yield return QuestSource.HusksKilled;
        else if (enemyName.EndsWith("Knight"))
            yield return QuestSource.KnightsKilled;
        else if (enemyName.EndsWith("Captain"))
            yield return QuestSource.CaptainsKilled;
        else if (enemyName.EndsWith("Spellweaver"))
            yield return QuestSource.SpellweaversKilled;
    }

    private static Quest[] Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("UntilYouFall.Quests.json")!;

        return JsonSerializer.Deserialize<Quest[]>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() },
            }
        )!;
    }
}
