using System.Reflection;
using UntilYouFallArchipelago;

internal static class StageDifficultyChecksTests
{
    public static void Run()
    {
        var catalog = StageDifficultyChecks.ActiveIds(new DifficultySettings(0, 3)).ToArray();
        Assert(
            catalog.SequenceEqual(Enumerable.Range(911501, 63).Select(value => (long)value)),
            "63 stable IDs with no dependency on the active starting tier"
        );
        Assert(
            !catalog.Intersect(ArchipelagoClient.BaseCheckIds.Concat(StageRewardChecks.ActiveIds(30))).Any(),
            "difficulty IDs cannot collide with existing locations"
        );
        for (var starting = 0; starting <= 3; starting++)
        for (var maximum = starting; maximum <= 3; maximum++)
        {
            var settings = new DifficultySettings(starting, maximum);
            Assert(
                StageDifficultyChecks.ActiveIds(settings).Count() == 21 * (maximum - starting),
                "only tiers strictly above starting and at most maximum exist"
            );
            for (var tier = -1; tier <= 4; tier++)
                Assert(
                    StageDifficultyChecks.IsActive(settings, tier) == (tier > starting && tier <= maximum),
                    "tutorial sentinel, starting, lower and unsupported tiers are inactive"
                );
        }
        var single = new GoalSettings(GoalMode.RunsMaxDifficulty, 1);
        var range = new DifficultySettings(0, 3);
        Assert(
            StageDifficultyChecks
                .ActiveIds(range, single)
                .SequenceEqual(catalog.Where(id => id != StageDifficultyChecks.LocationId(21, 3))),
            "a single max-difficulty run goal removes only the final stage on the max difficulty"
        );
        Assert(
            StageDifficultyChecks.ActiveIds(range, new GoalSettings(GoalMode.RunsMaxDifficulty, 2)).Count() == 63
                && StageDifficultyChecks.ActiveIds(range, new GoalSettings(GoalMode.Runs, 1)).Count() == 63,
            "other goals and repeated runs keep every difficulty check"
        );
        Assert(
            StageDifficultyChecks.ActiveIds(new DifficultySettings(3, 3), single).Count() == 0
                && !StageDifficultyChecks.IsGoalCheck(new DifficultySettings(3, 3), single, 21, 3),
            "no goal replacement when the maximum is the starting difficulty"
        );
        Assert(
            StageDifficultyChecks.Name(1, 2) == "Clear Stage 1 on Relentless"
                && StageDifficultyChecks.Name(7, 2) == "Clear Stage 7 - Defeat Boss 1 on Relentless"
                && StageDifficultyChecks.Name(14, 2) == "Clear Stage 14 - Defeat Boss 2 on Relentless"
                && StageDifficultyChecks.Name(21, 3) == "Clear Stage 21 - Defeat Final Boss on Nightmare",
            "difficulty names retain the same boss labels as weapon checks"
        );
        foreach (var pair in new[] { (0, 1), (22, 1), (1, 0), (1, 4) })
        {
            try
            {
                StageDifficultyChecks.LocationId(pair.Item1, pair.Item2);
                throw new Exception("invalid ID accepted");
            }
            catch (ArgumentOutOfRangeException) { }
        }
        CheckPersistence();
        Console.WriteLine(
            "PASS: stage difficulty range, exact ownership, stable IDs, deduplication and offline persistence"
        );
    }

    private static void CheckPersistence()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-stage-difficulty-" + Guid.NewGuid());
        var state = ApState.Open(directory, "difficulty-check-seed");
        var client = new ArchipelagoClient(directory, _ => { });
        ClientSessionTestHelpers.AttachState(client, state);
        ClientSessionTestHelpers.SetSettings(client, "Difficulty", new DifficultySettings(1, 3));
        try
        {
            state.UnlockDifficulty(910461);
            client.ClearStageDifficulty(1, 2);
            Assert(state.Checks.Count == 0, "locked difficulty never records a check");
            state.UnlockDifficulty(910463);
            client.ClearStageDifficulty(1, 2);
            Assert(state.Checks.Count == 0, "higher unlock does not unlock a lower difficulty check");
            client.ClearStageDifficulty(1, 3);
            client.ClearStageDifficulty(1, 3);
            Assert(state.Checks.SetEquals(new[] { 911543L }), "exact tier only, duplicate clear is deduplicated");
            state.UnlockDifficulty(910462);
            client.ClearStageDifficulty(1, 2);
            client.ClearStageDifficulty(21, 2);
            client.ClearStageDifficulty(1, 1);
            client.ClearStageDifficulty(1, 0);
            client.ClearStageDifficulty(1, -1);
            client.ClearStageDifficulty(1, 4);
            client.ClearStage(1, new[] { WeaponCatalog.All[0] });
            var expected = new[] { 911543L, 911522L, 911542L, StageChecks.LocationId(1, WeaponCatalog.All[0].Id) };
            Assert(state.Checks.SetEquals(expected), "independent weapon checks and no inactive tiers");
            Assert(
                ApState.Open(directory, "difficulty-check-seed").Checks.SetEquals(expected),
                "offline checks survive reconnect and resume"
            );
            Assert(ApState.Open(directory, "different-seed").Checks.Count == 0, "new seeds do not inherit clears");
            ClientSessionTestHelpers.SetSettings(client, "Difficulty", new DifficultySettings(1, 1));
            client.ClearStageDifficulty(2, 2);
            Assert(state.Checks.SetEquals(expected), "above maximum is ignored even with ownership");
        }
        finally
        {
            client.Disconnect();
            Directory.Delete(directory, true);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
