using UntilYouFallArchipelago;

internal static class EliteEncounterTrapRulesTests
{
    public static void Run()
    {
        var campaign = EliteEncounterTrapRules.CampaignGuid;
        var expected = new[]
        {
            (RoomGuid: "192dfaee1ac3a954694e918dbcf95179", EncounterGuid: "02476d73c059dfa4bb2f5949c25a8016"),
            (RoomGuid: "9890d71fa28039b45b6f14cce180a996", EncounterGuid: "a591cce70bb3fec4c911fd9c9fff7781"),
            (RoomGuid: "dce879e60c78372478100f21d3b0da72", EncounterGuid: "e85aa7e3af9973842afe42db2553ef75"),
        };
        Assert(
            EliteEncounterTrapRules.EliteEncounters.SequenceEqual(expected),
            "the uniform selection catalog contains exactly the three researched vanilla pairs"
        );
        for (var index = 0; index < StageChecks.Count; index++)
        {
            var candidates = EliteEncounterTrapRules
                .EliteEncounters.Where(pair => EliteEncounterTrapRules.CanSelectRoom(index, pair.RoomGuid))
                .ToArray();
            Assert(
                candidates.SequenceEqual(
                    index < EliteEncounterTrapRules.KnightExcludedFromIndex ? expected : expected.Skip(1)
                ),
                "replacement stages 1-13 allow all elites; stages 14-21 exclude Knight only"
            );
        }
        Assert(
            EliteEncounterTrapRules.CanSelectRoom(
                EliteEncounterTrapRules.KnightExcludedFromIndex - 1,
                expected[0].RoomGuid
            )
                && !EliteEncounterTrapRules.CanSelectRoom(
                    EliteEncounterTrapRules.KnightExcludedFromIndex,
                    expected[0].RoomGuid
                )
                && !EliteEncounterTrapRules.CanSelectRoom(20, expected[0].RoomGuid),
            "the one-based stage 14 boundary is KnightExcludedFromIndex, not its one-based number"
        );
        Assert(
            !EliteEncounterTrapRules.CanSelectRoom(-1, expected[1].RoomGuid)
                && !EliteEncounterTrapRules.CanSelectRoom(21, expected[1].RoomGuid)
                && !EliteEncounterTrapRules.CanSelectRoom(0, "unknown-room")
                && !EliteEncounterTrapRules.CanSelectRoom(0, null),
            "selection rejects invalid depths and non-elite room identities"
        );
        Assert(
            EliteEncounterTrapRules.ShouldReplace(true, campaign, 0, true, false, false),
            "enabled first normal room is replaced"
        );
        Assert(
            !EliteEncounterTrapRules.ShouldReplace(false, campaign, 0, true, false, false),
            "disabled test leaves the run unchanged"
        );
        foreach (var index in new[] { 1, 2, 5, 10, 20 })
            Assert(
                EliteEncounterTrapRules.ShouldReplace(true, campaign, index, true, false, false),
                "the next normal encounter may be at any valid depth"
            );
        foreach (var index in new[] { -1, 21, 100 })
            Assert(
                !EliteEncounterTrapRules.ShouldReplace(true, campaign, index, true, false, false),
                "invalid depths are excluded"
            );
        Assert(
            !EliteEncounterTrapRules.ShouldReplace(true, null, 0, true, false, false)
                && !EliteEncounterTrapRules.ShouldReplace(true, "tutorial", 0, true, false, false),
            "only the normal campaign is eligible"
        );
        Assert(
            !EliteEncounterTrapRules.ShouldReplace(true, campaign, 0, false, false, false)
                && !EliteEncounterTrapRules.ShouldReplace(true, campaign, 0, true, true, false)
                && !EliteEncounterTrapRules.ShouldReplace(true, campaign, 0, true, false, true),
            "hub, boss and already loading requests are excluded"
        );
        Console.WriteLine("PASS: elite target-stage pools, stage 14 boundary and boss/tutorial/loading guards");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception("Elite encounter test rules: " + message);
    }
}
