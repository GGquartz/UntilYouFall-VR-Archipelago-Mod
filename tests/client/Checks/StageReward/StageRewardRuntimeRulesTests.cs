using UntilYouFallArchipelago;

internal static class StageRewardRuntimeRulesTests
{
    public static void Run()
    {
        Assert(
            StageRewardRuntimeRules.CanOfferInRoom("run-1/room-2", null)
                && StageRewardRuntimeRules.CanOfferInRoom("run-1/room-3", "run-1/room-2")
                && !StageRewardRuntimeRules.CanOfferInRoom("run-1/room-2", "run-1/room-2"),
            "only the room that already completed a Stage Reward is suppressed"
        );

        var slots = new[] { false, true, false };
        Assert(
            StageRewardRuntimeRules.ReusableSlot("run-1/room-2", 911401, "run-1/room-2", 911401, 1, slots, 0) == 1,
            "same room and location reuse the persisted Aether slot"
        );
        Assert(
            StageRewardRuntimeRules.ReusableSlot("run-1/room-2", 911401, "run-1/room-2", 911401, 0, slots, 0) == 1,
            "same offer moves to an available Aether slot after the room is replayed"
        );
        Assert(
            StageRewardRuntimeRules.ReusableSlot("run-1/room-3", 911401, "run-1/room-2", 911401, 1, slots, 0) == null,
            "a different room cannot reuse the persisted offer"
        );
        Assert(
            StageRewardRuntimeRules.ReusableSlot("run-1/room-2", 911402, "run-1/room-2", 911401, 1, slots, 0) == null,
            "an advanced cursor cannot reuse the old location"
        );

        Assert(
            StageRewardRuntimeRules.SelectHistoryPlaceholder(new[] { true, false, false }, 0) == 1,
            "history uses a vanilla non-Aether reward from the same offer"
        );
        Assert(
            StageRewardRuntimeRules.SelectHistoryPlaceholder(new[] { false, true, false }, 1) == 0,
            "placeholder selection is stable"
        );
        Assert(
            StageRewardRuntimeRules.SelectHistoryPlaceholder(new[] { true, true }, 0) == null,
            "an all-Aether offer has no safe same-screen placeholder"
        );

        Assert(
            StageRewardRuntimeRules
                .UniqueCandidateSlots(new[] { true, false, true }, new[] { 10, 20, 30 })
                .SequenceEqual([true, false, true]),
            "distinct vanilla Aether assets remain eligible"
        );
        Assert(
            StageRewardRuntimeRules
                .UniqueCandidateSlots(new[] { true, false, true }, new[] { 10, 20, 10 })
                .SequenceEqual([false, false, false])
                && StageRewardRuntimeRules
                    .UniqueCandidateSlots(new[] { true, true }, new[] { 0, 40 })
                    .SequenceEqual([false, true]),
            "duplicate or null asset identities are rejected instead of ambiguously tagging rewards"
        );

        Console.WriteLine("PASS: Stage Reward offer reuse, history placeholder and unique vanilla asset selection");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception("Stage Reward runtime rules: " + message);
    }
}
