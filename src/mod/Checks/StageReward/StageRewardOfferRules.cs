namespace UntilYouFallArchipelago;

internal sealed record StageRewardOfferDecision(long LocationId, int EligibleAttempt, int AetherSlot);

internal static class StageRewardOfferRules
{
    public static bool ShouldOffer(
        bool connected,
        bool hasNextCheck,
        bool scoutReady,
        int aetherRewardCount,
        int roll,
        int eligibleAttempt,
        int checkIndex,
        int activeCount,
        bool forceOffer = false
    )
    {
        if (!connected || !hasNextCheck || !scoutReady || aetherRewardCount < 1)
            return false;
        if (roll is < 0 or > 99)
            throw new ArgumentOutOfRangeException(nameof(roll));

        return forceOffer || roll < StageRewardProgress.ChancePercent(eligibleAttempt, checkIndex, activeCount);
    }

    public static int? SelectAetherSlot(IReadOnlyList<bool> aetherSlots, int deterministicRoll)
    {
        if (deterministicRoll < 0)
            throw new ArgumentOutOfRangeException(nameof(deterministicRoll));

        var candidates = Enumerable.Range(0, aetherSlots.Count).Where(index => aetherSlots[index]).ToArray();
        return candidates.Length == 0 ? null : candidates[deterministicRoll % candidates.Length];
    }
}
