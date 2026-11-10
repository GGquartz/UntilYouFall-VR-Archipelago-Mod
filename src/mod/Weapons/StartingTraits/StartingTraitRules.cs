using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UntilYouFallArchipelago;

internal sealed record StartingTraitItem(long Id, string Name, string RewardGuid);

internal sealed record StartingTraitOffer(long[] Order, int Options, string[] ClearedRooms);

internal sealed record StartingTraitRun(string Key, long[] Offered, long[] Taken, int PickLimit, bool Skipped = false)
{
    [JsonIgnore]
    public long[] Remaining => Offered.Except(Taken).ToArray();

    [JsonIgnore]
    public bool Complete => Skipped || Taken.Length >= PickLimit || Taken.Length >= Offered.Length;
}

internal static class StartingTraitRules
{
    private const float SkipDistanceMeters = 3f;
    public const long ExtraPickId = ApItems.ExtraStartingTraitPickId;
    public const long ExtraChoiceId = ApItems.ExtraStartingTraitOptionId;
    private const int BaseOptionCount = 3;
    private const int BasePickCount = 1;

    public const int StagesPerReroll = 3;
    public static readonly IReadOnlyList<StartingTraitItem> All = Load();

    public static int MaxCount(long id) =>
        id switch
        {
            ExtraChoiceId => ApItems.ExtraStartingTraitOptionMaxCount,
            ExtraPickId => ApItems.ExtraStartingTraitPickMaxCount,
            _ => All.Any(trait => trait.Id == id) ? ApItems.StartingTraitCopies : 0,
        };

    public static StartingTraitRun Create(
        string key,
        IReadOnlyDictionary<long, int> counts,
        IEnumerable<long> eligible,
        Random random
    ) => Create(key, counts, eligible, CreateOffer(counts, random));

    public static StartingTraitOffer CreateOffer(IReadOnlyDictionary<long, int> counts, Random random)
    {
        var candidates = All.Where(trait => counts.GetValueOrDefault(trait.Id) > 0).Select(trait => trait.Id).ToArray();
        for (var index = candidates.Length - 1; index > 0; index--)
        {
            var other = random.Next(index + 1);
            (candidates[index], candidates[other]) = (candidates[other], candidates[index]);
        }

        return new StartingTraitOffer(candidates, ExtraOptions(counts), Array.Empty<string>());
    }

    public static StartingTraitOffer MigrateOffer(StartingTraitRun run, IReadOnlyDictionary<long, int> counts) =>
        new(
            run.Offered.Concat(All.Where(trait => counts.GetValueOrDefault(trait.Id) > 0).Select(trait => trait.Id))
                .Distinct()
                .ToArray(),
            ExtraOptions(counts),
            []
        );

    public static bool NeedsReroll(StartingTraitOffer offer, IReadOnlyDictionary<long, int> counts) =>
        offer.ClearedRooms.Length >= StagesPerReroll
        || ExtraOptions(counts) > offer.Options
        || All.Any(trait => counts.GetValueOrDefault(trait.Id) > 0 && !offer.Order.Contains(trait.Id));

    public static StartingTraitRun Create(
        string key,
        IReadOnlyDictionary<long, int> counts,
        IEnumerable<long> eligible,
        StartingTraitOffer offer
    )
    {
        var candidates = offer.Order.Where(id => eligible.Contains(id) && counts.GetValueOrDefault(id) > 0);
        counts.TryGetValue(ExtraPickId, out var picks);
        var optionCount = BaseOptionCount + ExtraOptions(counts);
        var pickLimit = BasePickCount + Math.Clamp(picks, 0, ApItems.ExtraStartingTraitPickMaxCount);

        return new StartingTraitRun(key, candidates.Take(optionCount).ToArray(), Array.Empty<long>(), pickLimit);
    }

    private static int ExtraOptions(IReadOnlyDictionary<long, int> counts) =>
        Math.Clamp(counts.GetValueOrDefault(ExtraChoiceId), 0, ApItems.ExtraStartingTraitOptionMaxCount);

    public static bool Valid(StartingTraitOffer offer) =>
        offer.Order != null
        && offer.ClearedRooms != null
        && offer.Options is >= 0 and <= ApItems.ExtraStartingTraitOptionMaxCount
        && offer.Order.Length <= All.Count
        && offer.Order.Distinct().Count() == offer.Order.Length
        && offer.Order.All(id => All.Any(trait => trait.Id == id))
        && offer.ClearedRooms.Length <= StagesPerReroll
        && offer.ClearedRooms.All(room => !string.IsNullOrWhiteSpace(room))
        && offer.ClearedRooms.Distinct().Count() == offer.ClearedRooms.Length;

    public static bool Available(StartingTraitRun run, long id) =>
        !run.Complete && run.Offered.Contains(id) && !run.Taken.Contains(id);

    public static StartingTraitRun Pick(StartingTraitRun run, long id) =>
        !Available(run, id) ? run : run with { Taken = run.Taken.Append(id).ToArray() };

    public static StartingTraitRun Skip(StartingTraitRun run) => run.Complete ? run : run with { Skipped = true };

    public static bool ShouldSkip(StartingTraitRun run, float forwardDistance) =>
        !run.Complete && forwardDistance >= SkipDistanceMeters;

    public static (float X, float Y) SlotOffset(int count, int index)
    {
        var row = count > 4 && index >= 3 ? 1 : 0;
        var columns =
            count <= 4 ? count
            : row == 0 ? 3
            : count - 3;
        var column = index - row * 3;

        return ((column - (columns - 1) * 0.5f) * 0.25f, row * 0.30f);
    }

    public static bool Valid(StartingTraitRun run) =>
        !string.IsNullOrWhiteSpace(run.Key)
        && run.Offered != null
        && run.Taken != null
        && run.PickLimit >= BasePickCount
        && run.PickLimit <= BasePickCount + ApItems.ExtraStartingTraitPickMaxCount
        && run.Offered.Length <= BaseOptionCount + ApItems.ExtraStartingTraitOptionMaxCount
        && run.Offered.Distinct().Count() == run.Offered.Length
        && run.Offered.All(id => All.Any(trait => trait.Id == id))
        && run.Taken.Length <= run.PickLimit
        && run.Taken.Distinct().Count() == run.Taken.Length
        && run.Taken.All(run.Offered.Contains);

    private static StartingTraitItem[] Load()
    {
        using var stream = Assembly
            .GetExecutingAssembly()
            .GetManifestResourceStream("UntilYouFall.StartingTraits.json")!;

        return JsonSerializer.Deserialize<StartingTraitItem[]>(stream)!;
    }
}
