using UntilYouFallArchipelago;

internal static class StageCheckMappingTests
{
    public static void Run()
    {
        for (var index = 0; index < StageChecks.Count; index++)
        {
            Assert(
                StageChecks.TryGetStage(StageChecks.CampaignGuid, index, true, out var stage) && stage == index + 1,
                "zero-based campaign index maps to the player's stage"
            );
        }
        Assert(
            !StageChecks.TryGetStage("b16668e6a16418848ab46f22c8817b9c", 0, true, out _),
            "tutorial cannot award stage checks"
        );
        Assert(!StageChecks.TryGetStage(null, 0, true, out _), "hub cannot award stage checks");
        Assert(!StageChecks.TryGetStage(StageChecks.CampaignGuid, 0, false, out _), "death cannot award a check");
        Assert(
            !StageChecks.TryGetStage(StageChecks.CampaignGuid, -1, true, out _)
                && !StageChecks.TryGetStage(StageChecks.CampaignGuid, 21, true, out _),
            "out-of-campaign indices are rejected"
        );
        Console.WriteLine("PASS: campaign index to stage mapping and tutorial/hub/death filtering");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
