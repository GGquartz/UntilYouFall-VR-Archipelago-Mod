using System.Reflection;
using UntilYouFallArchipelago;

internal static class StartingTraitRerollTests
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-starting-trait-reroll-" + Guid.NewGuid());
        try
        {
            var state = ApState.Open(directory, "seed-reroll");
            var pool = StartingTraitRules.All.Select(trait => trait.Id).ToArray();
            foreach (var item in pool.Take(8))
                state.SetStartingTraitItemCount(item, 1);
            var client = Client(directory, state);
            var first = client.PrepareStartingTraits("run-1", true, pool, new Random(7))!;
            Assert(client.TakeStartingTrait(first.Key, first.Offered[0]), "the first run can consume its pick");
            var random = new CountingRandom(99);
            var next = client.PrepareStartingTraits("run-2", true, pool, random)!;
            Assert(
                random.Calls == 0 && next.Offered.SequenceEqual(first.Offered),
                "abandon/new run cannot reroll the starting offer"
            );
            Assert(
                next.Taken.Length == 0 && !next.Skipped && !next.Complete,
                "a new run restores picks without changing its options"
            );
            Assert(client.SkipStartingTraits(next.Key), "skipping is still available");
            state = ApState.Open(directory, "seed-reroll");
            client = Client(directory, state);
            random = new CountingRandom(11);
            var restored = client.PrepareStartingTraits("run-3", true, pool, random)!;
            Assert(
                random.Calls == 0 && restored.Offered.SequenceEqual(first.Offered) && !restored.Skipped,
                "restart, death and skip cannot reroll the next run"
            );
            Assert(
                !client.RecordStartingTraitStageClear("wrong-run", 1)
                    && !client.RecordStartingTraitStageClear(restored.Key, 0)
                    && !client.RecordStartingTraitStageClear(restored.Key, 22),
                "invalid stages and stale run callbacks cannot earn credit"
            );
            Assert(
                client.RecordStartingTraitStageClear(restored.Key, 1)
                    && !client.RecordStartingTraitStageClear(restored.Key, 1),
                "last enemy and completion fallback count the room only once"
            );

            state = ApState.Open(directory, "seed-reroll");
            client = Client(directory, state);
            Assert(
                state.StartingTraitOffer!.ClearedRooms.Length == 1
                    && !client.RecordStartingTraitStageClear(restored.Key, 1),
                "stage credits and duplicate filtering survive reconnect"
            );
            var beforeFailure = state.StartingTraitOffer;
            var locked = new BlockedSaveFile(StatePath(state));
            try
            {
                try
                {
                    client.RecordStartingTraitStageClear(restored.Key, 2);
                    throw new Exception("save failure was ignored");
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                Assert(
                    ReferenceEquals(state.StartingTraitOffer, beforeFailure),
                    "a failed save rolls back stage credit in memory"
                );
            }
            finally
            {
                locked.Dispose();
            }
            Assert(client.RecordStartingTraitStageClear(restored.Key, 2), "failed credit can be retried");
            random = new CountingRandom(12);
            var secondRun = client.PrepareStartingTraits("run-4", true, pool, random)!;
            Assert(
                random.Calls == 0 && state.StartingTraitOffer!.ClearedRooms.Length == 2,
                "two cleared stages do not refresh the offer"
            );
            Assert(
                client.RecordStartingTraitStageClear(secondRun.Key, 1)
                    && state.StartingTraitOffer.ClearedRooms.Length == 3,
                "the same stage in a different run adds the third cumulative credit"
            );
            Assert(
                ReferenceEquals(client.PrepareStartingTraits(secondRun.Key, false, pool, random), secondRun)
                    && !client.RecordStartingTraitStageClear(secondRun.Key, 2),
                "the third clear never changes a current run or grows unbounded history"
            );
            random = new CountingRandom(13);
            var refreshed = client.PrepareStartingTraits("run-5", true, pool, random)!;
            Assert(
                random.Calls > 0 && state.StartingTraitOffer!.ClearedRooms.Length == 0,
                "the next run rolls once and resets the three-stage counter"
            );

            state.SetStartingTraitItemCount(StartingTraitRules.ExtraPickId, 1);
            random = new CountingRandom(14);
            var extraPick = client.PrepareStartingTraits("run-6", true, pool, random)!;
            Assert(
                random.Calls == 0 && extraPick.PickLimit == 2 && extraPick.Offered.SequenceEqual(refreshed.Offered),
                "Extra Pick increases picks without rerolling the options"
            );
            Assert(client.RecordStartingTraitStageClear(extraPick.Key, 1), "credit exists before an option upgrade");
            state.SetStartingTraitItemCount(StartingTraitRules.ExtraChoiceId, 1);
            Assert(
                ReferenceEquals(client.PrepareStartingTraits(extraPick.Key, false, pool, random), extraPick),
                "an option receipt leaves the current snapshot untouched"
            );
            random = new CountingRandom(15);
            var extraOption = client.PrepareStartingTraits("run-7", true, pool, random)!;
            Assert(
                random.Calls > 0
                    && extraOption.Offered.Length == 4
                    && state.StartingTraitOffer!.ClearedRooms.Length == 0,
                "Extra Option rerolls the next run and resets stage credit"
            );
            state.SetStartingTraitItemCount(pool[8], 1);
            Assert(
                ReferenceEquals(client.PrepareStartingTraits(extraOption.Key, true, pool, random), extraOption),
                "a new trait receipt never replaces the displayed run snapshot"
            );
            random = new CountingRandom(16);
            var newTrait = client.PrepareStartingTraits("run-8", true, pool, random)!;
            Assert(
                random.Calls > 0 && state.StartingTraitOffer!.Order.Contains(pool[8]),
                "a newly unlocked trait participates in the next fresh roll"
            );
            state.SetStartingTraitItemCount(pool[8], 1);
            random = new CountingRandom(17);
            var duplicate = client.PrepareStartingTraits("run-9", true, pool, random)!;
            Assert(
                random.Calls == 0 && duplicate.Offered.SequenceEqual(newTrait.Offered),
                "replayed receipts cannot keep rerolling offers"
            );

            var order = state.StartingTraitOffer!.Order.ToArray();
            var compatible = order.Last();
            var changedWeapon = client.PrepareStartingTraits("run-10", true, new[] { compatible }, random)!;
            Assert(
                random.Calls == 0
                    && changedWeapon.Offered.SequenceEqual([compatible])
                    && state.StartingTraitOffer.Order.SequenceEqual(order),
                "changing weapons filters the saved priority without rolling or losing candidates"
            );
            var originalWeapon = client.PrepareStartingTraits("run-11", true, pool, random)!;
            Assert(
                random.Calls == 0 && originalWeapon.Offered.SequenceEqual(newTrait.Offered),
                "returning to the original equipment restores the same options"
            );
            state.ResetItemHistory();
            foreach (var item in pool.Take(9))
                state.SetStartingTraitItemCount(item, 1);
            state.SetStartingTraitItemCount(StartingTraitRules.ExtraPickId, 1);
            state.SetStartingTraitItemCount(StartingTraitRules.ExtraChoiceId, 1);
            client.PrepareStartingTraits("run-12", true, pool, random);
            Assert(random.Calls == 0, "reconstructing receipt history does not invalidate the saved offer");
            Assert(
                ApState.Open(directory, "other-seed").StartingTraitOffer == null,
                "offer history is isolated by AP identity"
            );
            Assert(
                client.PrepareStartingTraits("unrecorded-restore", false, pool, random) == null,
                "an old unrecorded run receives no retroactive bonus"
            );

            state.SetStartingTraitItemCount(pool[9], 1);
            var previousRun = state.StartingTraitRun;
            var previousOffer = state.StartingTraitOffer;
            locked = new BlockedSaveFile(StatePath(state));
            try
            {
                try
                {
                    client.PrepareStartingTraits("failed-new-run", true, pool, new Random(19));
                    throw new Exception("new offer save failure was ignored");
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                Assert(
                    ReferenceEquals(state.StartingTraitRun, previousRun)
                        && ReferenceEquals(state.StartingTraitOffer, previousOffer),
                    "offer and per-run snapshot roll back together when saving fails"
                );
            }
            finally
            {
                locked.Dispose();
            }

            var legacyState = ApState.Open(directory, "legacy");
            foreach (var item in pool.Take(8))
                legacyState.SetStartingTraitItemCount(item, 1);
            var legacyRun = StartingTraitRules.Create(
                "legacy-run",
                legacyState.StartingTraitItemCounts,
                pool,
                new Random(20)
            );
            legacyRun = StartingTraitRules.Skip(StartingTraitRules.Pick(legacyRun, legacyRun.Offered[0]));
            legacyState.SetStartingTraitRun(legacyRun);
            var legacyPath = StatePath(legacyState);
            var legacyJson = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(legacyPath))!.AsObject();
            legacyJson.Remove("StartingTraitOffer");
            File.WriteAllText(legacyPath, legacyJson.ToJsonString());
            legacyState = ApState.Open(directory, "legacy");
            var legacyClient = Client(directory, legacyState);
            random = new CountingRandom(21);
            Assert(
                legacyClient.PrepareStartingTraits(legacyRun.Key, false, pool, random)!.Complete,
                "legacy restore keeps its consumed or skipped snapshot"
            );
            var migrated = legacyClient.PrepareStartingTraits("legacy-next-run", true, pool, random)!;
            Assert(
                random.Calls == 0
                    && migrated.Offered.SequenceEqual(legacyRun.Offered)
                    && migrated.Taken.Length == 0
                    && !migrated.Skipped
                    && legacyState.StartingTraitOffer != null,
                "a legacy save preserves its options without granting a free migration reroll"
            );
            Assert(
                !StartingTraitRules.Valid(new StartingTraitOffer([pool[0], pool[0]], 0, Array.Empty<string>()))
                    && !StartingTraitRules.Valid(new StartingTraitOffer([pool[0]], 4, Array.Empty<string>()))
                    && !StartingTraitRules.Valid(new StartingTraitOffer([pool[0]], 0, ["room", "room"]))
                    && !StartingTraitRules.Valid(new StartingTraitOffer([pool[0]], 0, ["a", "b", "c", "d"]))
                    && !StartingTraitRules.Valid(new StartingTraitOffer([999L], 0, Array.Empty<string>())),
                "invalid persisted priorities, option counts and room credits are rejected"
            );
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        Console.WriteLine(
            "PASS: starting trait anti-reroll, cumulative clears, unlocks, equipment, legacy persistence and save rollback"
        );
    }

    private static ArchipelagoClient Client(string directory, ApState state)
    {
        var client = new ArchipelagoClient(directory, _ => { });
        ClientSessionTestHelpers.AttachState(client, state);
        return client;
    }

    private static string StatePath(ApState state) =>
        (string)typeof(ApState).GetField("_path", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(state)!;

    private sealed class CountingRandom(int seed) : Random(seed)
    {
        public int Calls { get; private set; }

        public override int Next(int maxValue)
        {
            Calls++;
            return base.Next(maxValue);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception("Starting trait reroll: " + message);
    }
}
