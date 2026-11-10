using UntilYouFallArchipelago;

internal static class GoalRulesTests
{
    public static void Run()
    {
        Assert(GoalRules.Read(Slot("quests", 5)) == new GoalSettings(GoalMode.Quests, 5), "quests goal parsed");
        Assert(
            GoalRules.Read(Slot("runs_max_difficulty", 20)).Mode == GoalMode.RunsMaxDifficulty,
            "max difficulty goal parsed"
        );
        foreach (var bad in new[] { Slot("nope", 5), Slot("runs", 0), Slot("runs", 21) })
        {
            try
            {
                GoalRules.Read(bad);
                throw new Exception("bad goal accepted");
            }
            catch (InvalidDataException) { }
        }
        try
        {
            GoalRules.Read(new Dictionary<string, object>());
            throw new Exception("missing goal accepted");
        }
        catch (InvalidDataException) { }

        var runs = new GoalSettings(GoalMode.Runs, 3);
        Assert(!GoalRules.IsComplete(runs, true, true, 2, 9), "runs goal waits for the count");
        Assert(GoalRules.IsComplete(runs, false, false, 3, 0), "runs goal counts any difficulty");
        var max = new GoalSettings(GoalMode.RunsMaxDifficulty, 3);
        Assert(!GoalRules.IsComplete(max, true, true, 9, 2), "max difficulty goal counts only max runs");
        Assert(GoalRules.IsComplete(max, false, false, 3, 3), "max difficulty goal completes");
        Assert(GoalRules.IsComplete(new GoalSettings(GoalMode.Quests, 1), true, false, 0, 0), "quests goal");
        Assert(GoalRules.IsComplete(new GoalSettings(GoalMode.AllWeapons, 1), false, true, 0, 0), "all weapons goal");
        Assert(
            GoalRules.GetProgressText(new GoalSettings(GoalMode.Quests, 5), 12, 18, 0, 15, 0, 0, "Nightmare")
                == "Complete all quests (12/18)",
            "quest text"
        );
        Assert(
            GoalRules.GetProgressText(max, 0, 0, 0, 15, 9, 7, "Nightmare") == "Clear 3 runs on Nightmare (3/3)",
            "capped max text"
        );
        Assert(
            GoalRules.GetProgressText(new GoalSettings(GoalMode.Runs, 1), 0, 0, 0, 15, 0, 0, "Nightmare")
                == "Clear 1 run (0/1)",
            "singular"
        );
        Assert(
            GoalRules.GetProgressText(new GoalSettings(GoalMode.AllWeapons, 1), 0, 0, 4, 15, 0, 0, "x")
                == "Clear a run with all weapons (4/15)",
            "weapons text"
        );
        Assert(!GoalRules.ShouldSend(false, false, true), "goal requires a live connection");
        Assert(!GoalRules.ShouldSend(true, true, true), "goal is sent once per connection");
        Assert(!GoalRules.ShouldSend(true, false, false), "goal waits for the condition");
        Assert(GoalRules.ShouldSend(true, false, true), "goal sends");
    }

    private static Dictionary<string, object> Slot(string goal, int runs) =>
        new() { ["goal"] = goal, ["goal_runs"] = runs };

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
