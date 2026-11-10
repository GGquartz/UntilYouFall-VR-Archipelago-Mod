namespace UntilYouFallArchipelago;

internal enum GoalMode
{
    Quests,
    AllWeapons,
    RunsMaxDifficulty,
    Runs,
}

internal sealed record GoalSettings(GoalMode Mode, int Runs);

internal static class GoalRules
{
    public const int MaxRuns = OptionRanges.GoalRunsMaximum;

    public static GoalSettings Read(IDictionary<string, object> slotData)
    {
        if (
            !slotData.TryGetValue("goal", out var goal)
            || goal == null
            || !slotData.TryGetValue("goal_runs", out var runs)
            || runs == null
        )
            throw new InvalidDataException("Failed to find goal settings in AP slot data");

        var count = Convert.ToInt32(runs);
        if (count < OptionRanges.GoalRunsMinimum || count > MaxRuns)
            throw new InvalidDataException($"Invalid goal runs count in AP slot data: {count}");

        var mode = Convert.ToString(goal) switch
        {
            "quests" => GoalMode.Quests,
            "all_weapons" => GoalMode.AllWeapons,
            "runs_max_difficulty" => GoalMode.RunsMaxDifficulty,
            "runs" => GoalMode.Runs,
            var other => throw new InvalidDataException($"Unrecognized goal '{other}' in AP slot data"),
        };

        return new GoalSettings(mode, count);
    }

    public static bool IsComplete(
        GoalSettings goal,
        bool allQuestsDone,
        bool allWeaponsCleared,
        int runs,
        int maxDifficultyRuns
    ) =>
        goal.Mode switch
        {
            GoalMode.Quests => allQuestsDone,
            GoalMode.AllWeapons => allWeaponsCleared,
            GoalMode.RunsMaxDifficulty => maxDifficultyRuns >= goal.Runs,
            _ => runs >= goal.Runs,
        };

    public static string GetProgressText(
        GoalSettings goal,
        int questsDone,
        int questsTotal,
        int weaponsCleared,
        int weaponsTotal,
        int runs,
        int maxDifficultyRuns,
        string maxDifficultyName
    )
    {
        var runWording = goal.Runs == 1 ? "run" : "runs";

        return goal.Mode switch
        {
            GoalMode.Quests => $"Complete all quests ({questsDone}/{questsTotal})",
            GoalMode.AllWeapons => $"Clear a run with all weapons ({weaponsCleared}/{weaponsTotal})",
            GoalMode.RunsMaxDifficulty =>
                $"Clear {goal.Runs} {runWording} on {maxDifficultyName} ({Math.Min(maxDifficultyRuns, goal.Runs)}/{goal.Runs})",
            _ => $"Clear {goal.Runs} {runWording} ({Math.Min(runs, goal.Runs)}/{goal.Runs})",
        };
    }

    public static bool ShouldSend(bool connected, bool alreadySent, bool complete) =>
        connected && !alreadySent && complete;
}
