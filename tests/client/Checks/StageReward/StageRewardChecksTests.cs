using UntilYouFallArchipelago;

internal static class StageRewardChecksTests
{
    public static void Run()
    {
        Assert(
            StageRewardChecks.BaseId == 911400 && StageRewardChecks.MaximumCount == 30,
            "catalog constants match the APWorld contract"
        );
        Assert(
            StageRewardChecks.Name(1) == "Stage Reward 1" && StageRewardChecks.LocationId(30) == 911430,
            "names and IDs are stable"
        );
        Assert(
            StageRewardChecks.TryGetIndex(911401, out var first)
                && first == 1
                && StageRewardChecks.TryGetIndex(911430, out var last)
                && last == 30
                && !StageRewardChecks.TryGetIndex(911400, out _)
                && !StageRewardChecks.TryGetIndex(911431, out _),
            "reverse lookup accepts only the reserved range"
        );
        Assert(
            StageRewardChecks.ActiveIds(10).SequenceEqual(Enumerable.Range(1, 10).Select(StageRewardChecks.LocationId)),
            "active IDs are the requested prefix"
        );

        foreach (var count in new[] { 10, 20, 30 })
            Assert(
                StageRewardChecks.ReadCount(new Dictionary<string, object> { ["stage_reward_checks"] = count })
                    == count,
                $"slot count {count} parses"
            );
        foreach (var value in new object?[] { null, 9, 31, "twenty" })
        {
            var slot = new Dictionary<string, object>();
            if (value != null)
                slot["stage_reward_checks"] = value;
            try
            {
                StageRewardChecks.ReadCount(slot);
                throw new Exception("invalid count accepted");
            }
            catch (InvalidDataException) { }
        }

        foreach (var index in new[] { 0, 31 })
        {
            try
            {
                StageRewardChecks.LocationId(index);
                throw new Exception("invalid index accepted");
            }
            catch (ArgumentOutOfRangeException) { }
        }
        Console.WriteLine("PASS: Stage Reward catalog, active prefix and slot count validation");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception("Stage Reward checks: " + message);
    }
}
