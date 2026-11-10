using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UntilYouFallArchipelago;

internal static class StageRewardProgressTests
{
    public static void Run()
    {
        Assert(
            StageRewardProgress.NextLocationId(20, new HashSet<long>(), new HashSet<long>()) == 911401,
            "cursor starts at the first active check"
        );
        Assert(
            StageRewardProgress.NextLocationId(20, new HashSet<long> { 911401 }, new HashSet<long>()) == 911402,
            "offline local check advances the cursor"
        );
        Assert(
            StageRewardProgress.NextLocationId(20, new HashSet<long>(), new HashSet<long> { 911401, 911402 }) == 911403,
            "server confirmations advance the cursor"
        );
        Assert(
            StageRewardProgress.NextLocationId(
                10,
                Enumerable.Range(1, 10).Select(StageRewardChecks.LocationId).ToHashSet(),
                new HashSet<long>()
            ) == null,
            "cursor stops after the active prefix"
        );
        Assert(
            Enumerable
                .Range(1, 7)
                .Select(attempt => StageRewardProgress.ChancePercent(attempt, 1, 20))
                .SequenceEqual(new[] { 20, 30, 40, 50, 70, 100, 100 }),
            "pity follows the approved chances and caps at 100 percent"
        );
        foreach (var count in Enumerable.Range(10, 21))
        {
            for (var index = 1; index <= count; index++)
            {
                var cooldown = index <= 3 ? 0 : 3 * (int)Math.Ceiling(3.0 * (index - 3) / (count - 3));
                Assert(
                    Enumerable
                        .Range(1, cooldown)
                        .All(attempt => StageRewardProgress.ChancePercent(attempt, index, count) == 0),
                    $"check {index}/{count} waits through its complete cooldown"
                );
                Assert(
                    Enumerable
                        .Range(1, 7)
                        .Select(attempt => StageRewardProgress.ChancePercent(cooldown + attempt, index, count))
                        .SequenceEqual(new[] { 20, 30, 40, 50, 70, 100, 100 }),
                    $"check {index}/{count} uses the original chance curve after cooldown"
                );
            }
        }
        foreach (var (index, cooldown) in new[] { (3, 0), (4, 3), (8, 3), (9, 6), (14, 6), (15, 9), (20, 9) })
            Assert(
                StageRewardProgress.ChancePercent(cooldown + 1, index, 20) == 20
                    && (cooldown == 0 || StageRewardProgress.ChancePercent(cooldown, index, 20) == 0),
                $"default check {index} has the approved {cooldown}-opportunity cooldown"
            );
        foreach (var (count, first, second) in new[] { (10, 5, 7), (30, 12, 21) })
            Assert(
                StageRewardProgress.ChancePercent(4, first, count) == 20
                    && StageRewardProgress.ChancePercent(4, first + 1, count) == 0
                    && StageRewardProgress.ChancePercent(7, second, count) == 20
                    && StageRewardProgress.ChancePercent(7, second + 1, count) == 0,
                $"{count}-check seeds divide the remaining checks into thirds"
            );
        foreach (var (count, expectedTotal) in new[] { (10, 74.144), (20, 163.288), (30, 249.432) })
        {
            var total = 0.0;
            for (var index = 1; index <= count; index++)
            {
                var survival = 1.0;
                for (var attempt = 1; attempt <= 15; attempt++)
                {
                    total += survival;
                    survival *= 1 - StageRewardProgress.ChancePercent(attempt, index, count) / 100.0;
                }
            }
            Assert(
                Math.Abs(total - expectedTotal) < 0.000001,
                $"{count}-check pacing matches the approved expected number of eligible opportunities"
            );
        }

        ExpectInvalid(() => StageRewardProgress.ChancePercent(0, 1, 20));
        ExpectInvalid(() => StageRewardProgress.ChancePercent(1, 0, 20));
        ExpectInvalid(() => StageRewardProgress.ChancePercent(1, 21, 20));
        ExpectInvalid(() => StageRewardProgress.ChancePercent(1, 1, 9));
        ExpectInvalid(() => StageRewardProgress.ChancePercent(1, 1, 31));

        var directory = Path.Combine(Path.GetTempPath(), "uyf-stage-reward-state-" + Guid.NewGuid());
        try
        {
            var identity = "server/seed-A/team0/slot1";
            var state = ApState.Open(directory, identity);
            Assert(
                state.StageRewardPity == 0 && state.StageRewardOfferRoom == null,
                "new state starts without pity or offer"
            );
            state.IncrementStageRewardPity("run-1/room-1");
            state.IncrementStageRewardPity("run-1/room-2");
            state.SetStageRewardOffer("run-1/room-2", 911401, 1);
            var reopened = ApState.Open(directory, identity);
            Assert(
                reopened.StageRewardPity == 2
                    && reopened.StageRewardOfferRoom == "run-1/room-2"
                    && reopened.StageRewardOfferLocationId == 911401
                    && reopened.StageRewardOfferSlot == 1,
                "same seed and slot restore pity and stable offer"
            );
            Assert(
                reopened.IncrementStageRewardPity("run-1/room-2") == null && reopened.StageRewardPity == 2,
                "reloading the same reward screen does not advance pity"
            );
            reopened.ClearStageRewardOffer();
            Assert(
                reopened.StageRewardPity == 2 && reopened.StageRewardOfferRoom == null,
                "declining clears only the offer"
            );
            Assert(
                reopened.IncrementStageRewardPity("run-1/room-2") == null,
                "declining does not allow a new roll in the same room"
            );
            reopened.SetStageRewardOffer("run-1/room-3", 911401, 0);
            Assert(
                reopened.CompleteStageReward(911401)
                    && reopened.StageRewardPity == 0
                    && reopened.StageRewardOfferRoom == null
                    && reopened.Checks.Contains(911401)
                    && reopened.LastCompletedStageRewardRoom == "run-1/room-3",
                "completed crush atomically records the check and resets pity and offer"
            );
            Assert(
                ApState.Open(directory, identity).LastCompletedStageRewardRoom == "run-1/room-3",
                "completed Stage Reward room persists across reload"
            );
            Assert(!reopened.CompleteStageReward(911401), "duplicate completion is idempotent");
            Assert(
                ApState.Open(directory, "server/seed-B/team0/slot1").StageRewardPity == 0,
                "different seed is isolated"
            );

            var legacyIdentity = "server/legacy/team0/slot1";
            var legacyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(legacyIdentity)));
            File.WriteAllText(
                Path.Combine(directory, legacyHash + ".json"),
                JsonSerializer.Serialize(
                    new
                    {
                        Identity = legacyIdentity,
                        HandledCount = 0,
                        Checks = Array.Empty<long>(),
                    }
                )
            );
            var legacy = ApState.Open(directory, legacyIdentity);
            Assert(
                legacy.StageRewardPity == 0 && legacy.StageRewardOfferRoom == null,
                "legacy state loads safe Stage Reward defaults"
            );

            File.WriteAllText(
                Path.Combine(directory, legacyHash + ".json"),
                JsonSerializer.Serialize(
                    new
                    {
                        Identity = legacyIdentity,
                        HandledCount = 0,
                        Checks = new[] { 911401L },
                        StageRewardPity = 6,
                        StageRewardOfferRoom = "old-run/room-8",
                        StageRewardOfferLocationId = 911402L,
                        StageRewardOfferSlot = 1,
                    }
                )
            );
            legacy = ApState.Open(directory, legacyIdentity);
            Assert(
                legacy.StageRewardPity == 6
                    && legacy.Checks.Contains(911401)
                    && legacy.StageRewardOfferLocationId == 911402
                    && legacy.StageRewardOfferSlot == 1,
                "pre-cooldown state retains progress, pity and its active offer"
            );
            legacy.ClearStageRewardOffer();
            Assert(
                legacy.IncrementStageRewardPity("old-run/room-8") == null,
                "legacy offer room is remembered when the new room field is absent"
            );

            var pacing = ApState.Open(directory, "server/pacing/team0/slot1");
            for (var attempt = 0; attempt < 20; attempt++)
                pacing.IncrementStageRewardPity($"run-2/room-{attempt}");
            Assert(
                pacing.StageRewardPity == 15,
                "pity supports nine waiting opportunities followed by six chance attempts"
            );
            Assert(
                ApState.Open(directory, pacing.Identity).StageRewardPity == 15,
                "the maximum extended counter survives restart"
            );
            ExpectInvalid(() => pacing.IncrementStageRewardPity(" "));

            var failedDirectory = Path.Combine(directory, "failed-save");
            var failed = ApState.Open(failedDirectory, "server/failed-save/team0/slot1");
            Directory.Delete(failedDirectory);
            var rejected = false;
            try
            {
                failed.IncrementStageRewardPity("run-3/room-1");
            }
            catch (DirectoryNotFoundException)
            {
                rejected = true;
            }
            Assert(
                rejected && failed.StageRewardPity == 0 && failed.LastStageRewardAttemptRoom == null,
                "a failed save rolls back both the counter and the last attempted room"
            );
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        Console.WriteLine("PASS: Stage Reward cursor, pity and persistent offer state");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception("Stage Reward progress: " + message);
    }

    private static void ExpectInvalid(Action action)
    {
        var rejected = false;
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            rejected = true;
        }
        Assert(rejected, "invalid pacing input must be rejected");
    }
}
