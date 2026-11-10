namespace UntilYouFallArchipelago;

internal static class StageRewardProgress
{
    public const int MaximumAttempts = 15;
    private const int EarlyRewardCount = 3;
    private const int CooldownBandCount = 3;
    private const int AttemptsPerCooldownBand = 3;
    private static readonly int[] Chances = [20, 30, 40, 50, 70, 100];

    public static long? NextLocationId(int activeCount, IReadOnlySet<long> localChecks, IReadOnlySet<long> serverChecks)
    {
        foreach (var id in StageRewardChecks.ActiveIds(activeCount))
            if (!localChecks.Contains(id) && !serverChecks.Contains(id))
                return id;

        return null;
    }

    public static int ChancePercent(int eligibleAttempt, int checkIndex, int activeCount)
    {
        if (eligibleAttempt < 1)
            throw new ArgumentOutOfRangeException(nameof(eligibleAttempt));
        if (activeCount is < StageRewardChecks.MinimumCount or > StageRewardChecks.MaximumCount)
            throw new ArgumentOutOfRangeException(nameof(activeCount));
        if (checkIndex < 1 || checkIndex > activeCount)
            throw new ArgumentOutOfRangeException(nameof(checkIndex));

        var cooldown =
            checkIndex <= EarlyRewardCount
                ? 0
                : AttemptsPerCooldownBand
                    * (int)
                        Math.Ceiling(
                            (double)CooldownBandCount
                                * (checkIndex - EarlyRewardCount)
                                / (activeCount - EarlyRewardCount)
                        );
        var chanceAttempt = eligibleAttempt - cooldown;
        return chanceAttempt <= 0 ? 0 : Chances[Math.Min(chanceAttempt, Chances.Length) - 1];
    }
}
