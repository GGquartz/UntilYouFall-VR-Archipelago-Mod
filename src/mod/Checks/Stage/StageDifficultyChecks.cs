namespace UntilYouFallArchipelago;

internal static class StageDifficultyChecks
{
    public static long LocationId(int stage, int tier)
    {
        if (stage < 1 || stage > StageChecks.Count)
            throw new ArgumentOutOfRangeException(nameof(stage));
        if (tier < 1 || tier >= DifficultySettings.Names.Length)
            throw new ArgumentOutOfRangeException(nameof(tier));

        return LocationDefinitions.StageDifficultyBaseId + (tier - 1) * StageChecks.Count + stage;
    }

    public static string Name(int stage, int tier) =>
        $"{StageChecks.StageName(stage)} on {DifficultySettings.Names[tier]}";

    public static bool IsActive(DifficultySettings settings, int tier) =>
        tier > settings.Starting && settings.InRange(tier);

    public static bool IsGoalCheck(DifficultySettings settings, GoalSettings? goal, int stage, int tier) =>
        goal is { Mode: GoalMode.RunsMaxDifficulty, Runs: 1 }
        && settings.Maximum > settings.Starting
        && tier == settings.Maximum
        && stage == StageChecks.Count;

    public static IEnumerable<long> ActiveIds(DifficultySettings settings, GoalSettings? goal = null) =>
        Enumerable
            .Range(settings.Starting + 1, settings.Maximum - settings.Starting)
            .SelectMany(tier =>
                Enumerable
                    .Range(1, StageChecks.Count)
                    .Where(stage => !IsGoalCheck(settings, goal, stage, tier))
                    .Select(stage => LocationId(stage, tier))
            );
}
