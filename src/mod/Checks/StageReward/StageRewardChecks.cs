namespace UntilYouFallArchipelago;

internal static class StageRewardChecks
{
    public const long BaseId = LocationDefinitions.StageRewardBaseId;
    public const int MinimumCount = OptionRanges.StageRewardChecksMinimum;
    public const int MaximumCount = OptionRanges.StageRewardChecksMaximum;

    public static string Name(int index)
    {
        ValidateIndex(index);

        return $"Stage Reward {index}";
    }

    public static long LocationId(int index)
    {
        ValidateIndex(index);

        return BaseId + index;
    }

    public static bool TryGetIndex(long locationId, out int index)
    {
        var offset = locationId - BaseId;
        if (offset is < 1 or > MaximumCount)
        {
            index = 0;
            return false;
        }
        index = (int)offset;

        return true;
    }

    public static long[] ActiveIds(int count)
    {
        ValidateCount(count);

        return Enumerable.Range(1, count).Select(LocationId).ToArray();
    }

    public static int ReadCount(IDictionary<string, object> slotData)
    {
        if (!slotData.TryGetValue("stage_reward_checks", out var raw))
            throw new InvalidDataException("Missing stage reward check count in AP slot data");

        try
        {
            var count = Convert.ToInt32(raw);
            ValidateCount(count);

            return count;
        }
        catch (Exception exception)
            when (exception
                    is FormatException
                        or InvalidCastException
                        or OverflowException
                        or ArgumentOutOfRangeException
            )
        {
            throw new InvalidDataException("Invalid stage reward check count in AP slot data", exception);
        }
    }

    private static void ValidateIndex(int index)
    {
        if (index is < 1 or > MaximumCount)
            throw new ArgumentOutOfRangeException(nameof(index));
    }

    private static void ValidateCount(int count)
    {
        if (count is < MinimumCount or > MaximumCount)
            throw new ArgumentOutOfRangeException(nameof(count));
    }
}
