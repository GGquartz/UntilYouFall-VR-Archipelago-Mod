using UntilYouFallArchipelago;

internal static class StageRewardOfferRulesTests
{
    public static void Run()
    {
        Assert(
            StageRewardOfferRules.ShouldOffer(
                connected: true,
                hasNextCheck: true,
                scoutReady: true,
                aetherRewardCount: 1,
                roll: 19,
                eligibleAttempt: 1,
                checkIndex: 1,
                activeCount: 20
            ),
            "first opportunity accepts rolls below 20"
        );
        Assert(
            !StageRewardOfferRules.ShouldOffer(
                connected: true,
                hasNextCheck: true,
                scoutReady: true,
                aetherRewardCount: 1,
                roll: 20,
                eligibleAttempt: 1,
                checkIndex: 1,
                activeCount: 20
            ),
            "chance boundary is exclusive"
        );
        Assert(
            StageRewardOfferRules.ShouldOffer(
                connected: true,
                hasNextCheck: true,
                scoutReady: true,
                aetherRewardCount: 1,
                roll: 99,
                eligibleAttempt: 6,
                checkIndex: 1,
                activeCount: 20
            ),
            "sixth opportunity is guaranteed"
        );
        Assert(
            StageRewardOfferRules.ShouldOffer(
                connected: true,
                hasNextCheck: true,
                scoutReady: true,
                aetherRewardCount: 1,
                roll: 99,
                eligibleAttempt: 1,
                checkIndex: 20,
                activeCount: 20,
                forceOffer: true
            ),
            "debug force accepts every eligible Aether opportunity"
        );
        Assert(
            !StageRewardOfferRules.ShouldOffer(
                connected: false,
                hasNextCheck: true,
                scoutReady: true,
                aetherRewardCount: 1,
                roll: 99,
                eligibleAttempt: 1,
                checkIndex: 20,
                activeCount: 20,
                forceOffer: true
            )
                && !StageRewardOfferRules.ShouldOffer(
                    connected: true,
                    hasNextCheck: false,
                    scoutReady: true,
                    aetherRewardCount: 1,
                    roll: 99,
                    eligibleAttempt: 1,
                    checkIndex: 20,
                    activeCount: 20,
                    forceOffer: true
                )
                && !StageRewardOfferRules.ShouldOffer(
                    connected: true,
                    hasNextCheck: true,
                    scoutReady: false,
                    aetherRewardCount: 1,
                    roll: 99,
                    eligibleAttempt: 1,
                    checkIndex: 20,
                    activeCount: 20,
                    forceOffer: true
                )
                && !StageRewardOfferRules.ShouldOffer(
                    connected: true,
                    hasNextCheck: true,
                    scoutReady: true,
                    aetherRewardCount: 0,
                    roll: 99,
                    eligibleAttempt: 1,
                    checkIndex: 20,
                    activeCount: 20,
                    forceOffer: true
                ),
            "debug force does not bypass eligibility gates"
        );
        Assert(
            !StageRewardOfferRules.ShouldOffer(
                connected: false,
                hasNextCheck: true,
                scoutReady: true,
                aetherRewardCount: 1,
                roll: 0,
                eligibleAttempt: 6,
                checkIndex: 1,
                activeCount: 20
            )
                && !StageRewardOfferRules.ShouldOffer(
                    connected: true,
                    hasNextCheck: false,
                    scoutReady: true,
                    aetherRewardCount: 1,
                    roll: 0,
                    eligibleAttempt: 6,
                    checkIndex: 1,
                    activeCount: 20
                )
                && !StageRewardOfferRules.ShouldOffer(
                    connected: true,
                    hasNextCheck: true,
                    scoutReady: false,
                    aetherRewardCount: 1,
                    roll: 0,
                    eligibleAttempt: 6,
                    checkIndex: 1,
                    activeCount: 20
                )
                && !StageRewardOfferRules.ShouldOffer(
                    connected: true,
                    hasNextCheck: true,
                    scoutReady: true,
                    aetherRewardCount: 0,
                    roll: 0,
                    eligibleAttempt: 6,
                    checkIndex: 1,
                    activeCount: 20
                ),
            "every eligibility gate is required and no-Aether screens are ineligible"
        );

        Assert(
            !StageRewardOfferRules.ShouldOffer(
                connected: true,
                hasNextCheck: true,
                scoutReady: true,
                aetherRewardCount: 1,
                roll: 0,
                eligibleAttempt: 9,
                checkIndex: 20,
                activeCount: 20
            )
                && StageRewardOfferRules.ShouldOffer(
                    connected: true,
                    hasNextCheck: true,
                    scoutReady: true,
                    aetherRewardCount: 1,
                    roll: 19,
                    eligibleAttempt: 10,
                    checkIndex: 20,
                    activeCount: 20
                )
                && !StageRewardOfferRules.ShouldOffer(
                    connected: true,
                    hasNextCheck: true,
                    scoutReady: true,
                    aetherRewardCount: 1,
                    roll: 20,
                    eligibleAttempt: 10,
                    checkIndex: 20,
                    activeCount: 20
                )
                && StageRewardOfferRules.ShouldOffer(
                    connected: true,
                    hasNextCheck: true,
                    scoutReady: true,
                    aetherRewardCount: 1,
                    roll: 99,
                    eligibleAttempt: 15,
                    checkIndex: 20,
                    activeCount: 20
                ),
            "late rewards wait nine eligible opportunities before the original chance curve"
        );

        var slots = new[] { false, true, false, true };
        Assert(
            StageRewardOfferRules.SelectAetherSlot(slots, 0) == 1
                && StageRewardOfferRules.SelectAetherSlot(slots, 1) == 3
                && StageRewardOfferRules.SelectAetherSlot(slots, 2) == 1,
            "selection is deterministic and limited to Aether slots"
        );
        Assert(
            StageRewardOfferRules.SelectAetherSlot(new[] { false, false }, 0) == null,
            "selection fails without an Aether reward"
        );
        var decision = new StageRewardOfferDecision(911401, 2, 3);
        Assert(
            decision.LocationId == 911401 && decision.EligibleAttempt == 2 && decision.AetherSlot == 3,
            "offer decision retains the stable runtime inputs"
        );
        Console.WriteLine("PASS: Stage Reward eligibility, pity boundary and Aether-only slot selection");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception("Stage Reward offer rules: " + message);
    }
}
