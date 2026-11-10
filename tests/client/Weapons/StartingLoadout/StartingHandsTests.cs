using UntilYouFallArchipelago;

internal static class StartingHandsTests
{
    public static void Run()
    {
        var expected = new[]
        {
            ("both", StartingHands.Both),
            ("left_only", StartingHands.LeftOnly),
            ("right_only", StartingHands.RightOnly),
        };
        foreach (var (value, mode) in expected)
        {
            var slot = new Dictionary<string, object> { ["starting_hands"] = value };
            Assert(StartingHandsRules.Read(slot) == mode, $"{value} parses");
        }
        foreach (
            var slot in new[]
            {
                [],
                new Dictionary<string, object> { ["starting_hands"] = "unknown" },
            }
        )
        {
            try
            {
                StartingHandsRules.Read(slot);
                throw new Exception("invalid starting hands accepted");
            }
            catch (InvalidDataException) { }
        }

        Assert(
            ApItems.LeftHandId == 910003 && ApItems.RightHandId == 910004,
            "hand item IDs match the APWorld contract"
        );
        Assert(
            ApItems.IsHand(910003) && ApItems.IsHand(910004) && !ApItems.IsHand(910002),
            "only hand items are classified as hands"
        );

        var directory = Path.Combine(Path.GetTempPath(), "uyf-hand-state-test-" + Guid.NewGuid());
        try
        {
            var state = ApState.Open(directory, "server/seed-A/team0/slot1");
            state.UnlockHand(ApItems.LeftHandId);
            state.UnlockHand(ApItems.LeftHandId);
            state.CommitItem(0);
            state.UnlockWeapon(910019);
            state.CommitItem(1);
            state.CommitItem(2);
            var reloaded = ApState.Open(directory, "server/seed-A/team0/slot1");
            Assert(
                reloaded.HandUnlocks.SetEquals(new[] { ApItems.LeftHandId }) && reloaded.HandledCount == 3,
                "hand, weapon and consumable cursor persist sequentially across reconnect"
            );
            Assert(
                ApState.Open(directory, "server/seed-B/team0/slot1").HandUnlocks.Count == 0
                    && ApState.Open(directory, "server/seed-A/team0/slot2").HandUnlocks.Count == 0,
                "hand ownership is isolated by seed and slot"
            );
            try
            {
                state.UnlockHand(910019);
                throw new Exception("non-hand unlock accepted");
            }
            catch (ArgumentOutOfRangeException) { }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        Console.WriteLine("PASS: starting hands slot parsing, IDs, durable ownership and sequential cursor");
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok)
            throw new Exception("Starting hands: " + message);
    }
}
