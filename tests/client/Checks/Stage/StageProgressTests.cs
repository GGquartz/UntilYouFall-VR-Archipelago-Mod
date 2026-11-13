using System.Reflection;
using UntilYouFallArchipelago;

internal static class StageProgressTests
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-stage-progress-" + Guid.NewGuid());
        try
        {
            var client = new ArchipelagoClient(directory, _ => { });
            Assert(client.WeaponStageProgress(910012) == null, "no progress without AP state");
            Assert(client.DifficultyStageProgress(3) == null, "no difficulty progress without AP state");
            var state = ApState.Open(directory, "stage-progress-seed");
            ClientSessionTestHelpers.AttachState(client, state);
            var confirmed = ClientSessionTestHelpers.Progress(client);
            foreach (var weapon in WeaponCatalog.All)
                Assert(client.WeaponStageProgress(weapon.Id) == "Stages Cleared 0/21", "empty weapon counter");
            Assert(client.WeaponStageProgress(-1) == null, "unknown weapon has no counter");
            foreach (var stage in new[] { 1, 3 })
                state.RecordCheck(StageChecks.LocationId(stage, 910012));
            foreach (var stage in new[] { 3, 21 })
                confirmed.HandleCheckedLocations([StageChecks.LocationId(stage, 910012)], false);
            foreach (var goal in Enum.GetValues<GoalMode>())
            {
                ClientSessionTestHelpers.SetSettings(client, "Goal", new GoalSettings(goal, 5));
                Assert(
                    client.WeaponStageProgress(910012) == "Stages Cleared 2/21",
                    "released/server-only checks are not counted, gaps and all goals"
                );
            }
            client.Disconnect();
            Assert(client.WeaponStageProgress(910012) == "Stages Cleared 2/21", "available offline");
            Assert(client.WeaponStageProgress(910013) == "Stages Cleared 0/21", "weapon isolation");
            foreach (var stage in Enumerable.Range(1, StageChecks.Count))
                state.RecordCheck(StageChecks.LocationId(stage, 910012));
            Assert(client.WeaponStageProgress(910012) == "Stages Cleared 21/21", "full counter capped at 21");

            foreach (var stage in new[] { 1, 3 })
                state.RecordCheck(StageDifficultyChecks.LocationId(stage, 2));
            foreach (var stage in new[] { 3, 21 })
                confirmed.HandleCheckedLocations([StageDifficultyChecks.LocationId(stage, 2)], false);
            for (var starting = 0; starting < 4; starting++)
            for (var maximum = starting; maximum < 4; maximum++)
            {
                ClientSessionTestHelpers.SetSettings(client, "Difficulty", new DifficultySettings(starting, maximum));
                for (var tier = -1; tier <= 4; tier++)
                {
                    var expected =
                        tier <= starting || tier > maximum ? null
                        : tier == 2 ? "Stages Cleared 2/21"
                        : "Stages Cleared 0/21";
                    Assert(client.DifficultyStageProgress(tier) == expected, "difficulty existence and isolation");
                }
            }
            ClientSessionTestHelpers.SetSettings(client, "Difficulty", new DifficultySettings(0, 2));
            ClientSessionTestHelpers.SetSettings(client, "Goal", new GoalSettings(GoalMode.RunsMaxDifficulty, 1));
            Assert(
                client.DifficultyStageProgress(2) == "Stages Cleared 2/20",
                "single max-difficulty goal excludes its final-stage check from progress"
            );
            ClientSessionTestHelpers.SetSettings(client, "Goal", new GoalSettings(GoalMode.RunsMaxDifficulty, 2));
            Assert(
                client.DifficultyStageProgress(2) == "Stages Cleared 2/21",
                "repeated-run goal retains its final-stage check"
            );
            var restored = new ArchipelagoClient(directory, _ => { });
            ClientSessionTestHelpers.AttachState(restored, ApState.Open(directory, "stage-progress-seed"));
            Assert(restored.WeaponStageProgress(910012) == "Stages Cleared 21/21", "persisted local checks");
            ClientSessionTestHelpers.AttachState(restored, ApState.Open(directory, "different-seed"));
            Assert(restored.WeaponStageProgress(910012) == "Stages Cleared 0/21", "new seed isolation");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
