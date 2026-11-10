using System.Reflection;
using System.Text.Json;
using Archipelago.MultiClient.Net.Models;
using UntilYouFallArchipelago;

internal static class StartingTraitTests
{
    public static void Run()
    {
        Assert(StartingTraitRules.All.Count == 28, "shared catalog has 28 traits");
        var pool = StartingTraitRules.All.Select(trait => trait.Id).ToArray();
        var counts = pool.ToDictionary(id => id, _ => 1);
        var offer = StartingTraitRules.Create("run-1", counts, pool.Concat(pool), new Random(7));
        Assert(offer.Offered.Length == 3 && offer.PickLimit == 1, "default three offers and one pick");
        Assert(offer.Offered.Distinct().Count() == 3, "duplicate candidates do not duplicate offers");
        var skippedJson = JsonSerializer.Serialize(
            new
            {
                offer.Key,
                offer.Offered,
                offer.Taken,
                offer.PickLimit,
                Skipped = true,
            }
        );
        var skippedSave = JsonSerializer.Deserialize<StartingTraitRun>(skippedJson)!;
        Assert(
            skippedSave.Skipped && skippedSave.Complete && skippedSave.Taken.Length == 0,
            "a saved skipped offer is complete without claiming a picked trait"
        );
        var legacyJson = JsonSerializer.Serialize(
            new
            {
                offer.Key,
                offer.Offered,
                offer.Taken,
                offer.PickLimit,
            }
        );
        var legacySave = JsonSerializer.Deserialize<StartingTraitRun>(legacyJson)!;
        Assert(
            !legacySave.Skipped && !legacySave.Complete && StartingTraitRules.Valid(legacySave),
            "a save without the new skipped flag still opens its unconsumed offer"
        );
        Assert(
            !StartingTraitRules.ShouldSkip(offer, -5f)
                && !StartingTraitRules.ShouldSkip(offer, 0f)
                && !StartingTraitRules.ShouldSkip(offer, 2.999f)
                && StartingTraitRules.ShouldSkip(offer, 3f)
                && StartingTraitRules.ShouldSkip(offer, 4f),
            "skip requires at least three meters forward"
        );
        var expectedRows = new[] { new[] { 1 }, [2], [3], [4], [3, 2], [3, 3] };
        for (var count = 1; count <= 6; count++)
        {
            var offsets = Enumerable
                .Range(0, count)
                .Select(index => StartingTraitRules.SlotOffset(count, index))
                .ToArray();
            Assert(offsets.Distinct().Count() == count, "each crystal has its own position");
            var rows = offsets.GroupBy(offset => offset.Y).OrderBy(row => row.Key).ToArray();
            Assert(
                rows.Select(row => row.Count()).SequenceEqual(expectedRows[count - 1]),
                "one row for up to four, then three plus two or three plus three"
            );
            Assert(
                offsets.Min(offset => offset.X) >= -0.375f && offsets.Max(offset => offset.X) <= 0.375f,
                "compact layout stays within a 75cm horizontal span"
            );
            Assert(
                rows[0].Key == 0f && (rows.Length == 1 || rows[1].Key == 0.30f),
                "the second row is 30cm above the first"
            );
            foreach (var row in rows)
            {
                var columns = row.Select(offset => offset.X).Order().ToArray();
                Assert(Math.Abs(columns.Sum()) < 0.0001f, "each row is independently centered");
                Assert(
                    columns.Zip(columns.Skip(1), (left, right) => right - left).All(gap => gap == 0.25f),
                    "neighboring crystals are 25cm apart"
                );
            }
        }
        counts[StartingTraitRules.ExtraPickId] = 1;
        counts[StartingTraitRules.ExtraChoiceId] = 3;
        offer = StartingTraitRules.Create("run-1", counts, pool, new Random(7));
        Assert(offer.Offered.Length == 6 && offer.PickLimit == 2, "maximum six offers and two picks");
        var picked = StartingTraitRules.Pick(offer, offer.Offered[0]);
        Assert(
            picked.Offered.SequenceEqual(offer.Offered) && picked.Remaining.Length == 5,
            "first pick keeps the original offer set"
        );
        Assert(!picked.Complete && picked.Taken.Length == 1, "second pick remains available");
        Assert(
            !StartingTraitRules.Available(picked, picked.Taken[0])
                && StartingTraitRules.Available(picked, picked.Remaining[0]),
            "native respawn cannot recreate the consumed crystal"
        );
        Assert(StartingTraitRules.Pick(picked, picked.Remaining[0]).Complete, "second pick finishes");
        Assert(StartingTraitRules.Pick(picked, picked.Taken[0]) == picked, "same crystal cannot be picked twice");
        var skipped = StartingTraitRules.Skip(picked);
        Assert(
            skipped.Complete
                && skipped.Skipped
                && skipped.Taken.SequenceEqual(picked.Taken)
                && skipped.Offered.SequenceEqual(picked.Offered)
                && StartingTraitRules.Valid(skipped),
            "skipping the remaining offer retains actual picks and the original set"
        );
        Assert(
            skipped.Offered.All(id => !StartingTraitRules.Available(skipped, id))
                && !StartingTraitRules.ShouldSkip(skipped, 5f)
                && ReferenceEquals(StartingTraitRules.Skip(skipped), skipped),
            "a skipped offer cannot be taken or skipped twice"
        );
        var small = StartingTraitRules.Create("run-2", counts, pool.Take(1), new Random(7));
        Assert(
            small.Offered.Length == 1 && StartingTraitRules.Pick(small, pool[0]).Complete,
            "a one-trait pool never waits for a nonexistent second pick"
        );
        Assert(
            StartingTraitRules.Create("run-3", new Dictionary<long, int>(), pool, new Random(7)).Complete,
            "no owned traits means no offer"
        );

        var directory = Path.Combine(Path.GetTempPath(), "uyf-starting-traits-" + Guid.NewGuid());
        try
        {
            var state = ApState.Open(directory, "seed-A");
            state.SetStartingTraitItemCount(pool[0], 1);
            state.SetStartingTraitItemCount(pool[0], 1);
            state.SetStartingTraitItemCount(StartingTraitRules.ExtraChoiceId, 8);
            state.SetStartingTraitRun(picked);
            state.CommitItem(0);
            var restored = ApState.Open(directory, "seed-A");
            Assert(
                restored.StartingTraitItemCounts[pool[0]] == 1
                    && restored.StartingTraitItemCounts[StartingTraitRules.ExtraChoiceId] == 3,
                "durable unlocks are idempotent and capped"
            );
            Assert(
                restored.StartingTraitRun!.Offered.SequenceEqual(offer.Offered)
                    && restored.StartingTraitRun.Taken.SequenceEqual(picked.Taken),
                "restore keeps set and used picks"
            );
            Assert(ApState.Open(directory, "seed-B").StartingTraitRun == null, "identity isolates run offers");
            restored.ResetItemHistory();
            Assert(
                restored.StartingTraitItemCounts.Count == 0 && restored.StartingTraitRun!.Taken.Length == 1,
                "receipt reset cannot reopen a consumed run pick"
            );
            Assert(ApState.Open(directory, "seed-A").StartingTraitItemCounts.Count == 0, "reset persists");
            var client = new ArchipelagoClient(directory, _ => { });
            ClientSessionTestHelpers.AttachState(client, restored);
            ClientSessionTestHelpers.SetLoggedIn(client, true);
            void Record(long item, int index) =>
                client.HandleReceivedItems(index, [new NetworkItem { Item = item }], false);
            Record(pool[0], 10);
            Record(pool[0], 10);
            for (var index = 11; index < 15; index++)
                Record(StartingTraitRules.ExtraChoiceId, index);
            Assert(
                client.StartingTraitItemCounts[pool[0]] == 1
                    && client.StartingTraitItemCounts[StartingTraitRules.ExtraChoiceId] == 3,
                "receipt replay deduplicates history indices and caps permanent counts"
            );
            Assert(
                client.PrepareStartingTraits("old-unrecorded-run", false, pool, new Random(2)) == null,
                "an existing run without an offer gets no retroactive bonus"
            );
            var current = client.PrepareStartingTraits("new-run", true, pool, new Random(2))!;
            Record(pool[1], 15);
            var unchanged = client.PrepareStartingTraits("new-run", true, pool, new Random(99))!;
            Assert(unchanged.Offered.SequenceEqual(current.Offered), "late unlock and reconnect cannot reroll offers");
            Assert(
                !client.TakeStartingTrait("other-run", pool[0])
                    && client.TakeStartingTrait("new-run", pool[0])
                    && !client.TakeStartingTrait("new-run", pool[0]),
                "only a matching unconsumed run pick commits"
            );
            restored.SetStartingTraitRun(picked);
            Assert(
                !client.SkipStartingTraits("other-run")
                    && client.SkipStartingTraits(picked.Key)
                    && !client.SkipStartingTraits(picked.Key),
                "skip commits once and only for the current run"
            );
            var skippedReload = ApState.Open(directory, "seed-A").StartingTraitRun!;
            Assert(
                skippedReload.Skipped && skippedReload.Complete && skippedReload.Taken.SequenceEqual(picked.Taken),
                "skip is durable without applying or recording additional traits"
            );
            Assert(
                client.PrepareStartingTraits(picked.Key, false, pool, new Random(2))!.Complete
                    && !client.TakeStartingTrait(picked.Key, picked.Remaining[0]),
                "reconnect and restore cannot reopen a skipped offer"
            );
            Assert(
                !client.PrepareStartingTraits("next-run", true, pool, new Random(2))!.Skipped,
                "a new run gets its own unskipped offer"
            );
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        Console.WriteLine("PASS: starting traits catalog, compact layout, limits, durable picks, skip and restore");
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok)
            throw new Exception("Starting traits: " + message);
    }
}
