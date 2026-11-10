using UntilYouFallArchipelago;

internal static class EnemySpawnTrapRulesTests
{
    public static void Run()
    {
        var strongest = EnemySpawnTrapRules.Strongest(
            new[]
            {
                new EnemyTrapCandidate("husk", "Lesser Husk", 1, false),
                new EnemyTrapCandidate("knight", "Rokari Knight", 4, false),
                new EnemyTrapCandidate("boss", "Aether Horror", 10, true),
                new EnemyTrapCandidate("captain", "Rokari Captain", 6, false),
            }
        );
        Assert(strongest?.Guid == "captain", "highest challenge supported non-boss enemy is selected");

        var tied = EnemySpawnTrapRules.Strongest(
            new[]
            {
                new EnemyTrapCandidate("z-guid", "Knight Z", 4, false),
                new EnemyTrapCandidate("a-guid", "Knight A", 4, false),
            }
        );
        Assert(tied?.Guid == "a-guid", "equal challenge selection is deterministic");
        Assert(
            EnemySpawnTrapRules.Strongest(new[] { new EnemyTrapCandidate("boss", "Aether Horror", 10, true) }) == null,
            "excluded enemies cannot become templates"
        );
        Assert(
            EnemySpawnTrapRules.IsExcludedEnemy(910307)
                && EnemySpawnTrapRules.IsExcludedEnemy(910310)
                && !EnemySpawnTrapRules.IsExcludedEnemy(910302),
            "boss definitions are excluded from template selection"
        );
        Assert(
            EnemySpawnTrapRules.CanCaptureRoom(true, false)
                && !EnemySpawnTrapRules.CanCaptureRoom(true, true)
                && !EnemySpawnTrapRules.CanCaptureRoom(false, false),
            "templates are captured only in non-boss encounters"
        );

        var placements = EnemySpawnTrapRules.PlacementOffsets;
        Assert(placements.Length == 43, "forward positions and broad side/back fallbacks are tested");
        Assert(
            placements[0] == new EnemyTrapOffset(3f, 0f)
                && placements[1] == new EnemyTrapOffset(3f, 25f)
                && placements[2] == new EnemyTrapOffset(3f, -25f),
            "closest visible forward positions are preferred first"
        );
        Assert(
            placements.Contains(new EnemyTrapOffset(2.5f, 75f))
                && placements.Contains(new EnemyTrapOffset(2.5f, -105f))
                && placements[^1] == new EnemyTrapOffset(5.5f, 180f),
            "fallbacks include nearby side positions and the space behind the player"
        );

        Assert(
            Math.Abs(EnemySpawnTrapRules.CapsuleBottomCenterY(0f, 0.55f) - 0.60f) < 0.001f,
            "clearance capsule starts above a walkable floor instead of touching it"
        );

        Assert(
            EnemySpawnTrapRules.FailureMessage(EnemyTrapSpawnFailure.InRuneforge) == "Spawn Failed (In Runeforge)"
                && EnemySpawnTrapRules.FailureMessage(EnemyTrapSpawnFailure.SceneLoading)
                    == "Spawn Failed (Scene Loading)"
                && EnemySpawnTrapRules.FailureMessage(EnemyTrapSpawnFailure.NoActiveEncounter)
                    == "Spawn Failed (No Active Encounter)"
                && EnemySpawnTrapRules.FailureMessage(EnemyTrapSpawnFailure.BossRoom) == "Spawn Failed (Boss Room)"
                && EnemySpawnTrapRules.FailureMessage(EnemyTrapSpawnFailure.NoEnemyTemplate)
                    == "Spawn Failed (No Enemy Template)"
                && EnemySpawnTrapRules.FailureMessage(EnemyTrapSpawnFailure.NoActiveCamera)
                    == "Spawn Failed (No Active Camera)"
                && EnemySpawnTrapRules.FailureMessage(EnemyTrapSpawnFailure.NoLocationFound)
                    == "Spawn Failed (No Location Found)"
                && EnemySpawnTrapRules.FailureMessage(EnemyTrapSpawnFailure.InternalError)
                    == "Spawn Failed (Unknown Error)",
            "spawn failures have short technical-dialog messages"
        );

        Assert(
            !EnemySpawnTrapRules.IsBurstDuplicate(1000, null)
                && EnemySpawnTrapRules.IsBurstDuplicate(1999, 1000)
                && !EnemySpawnTrapRules.IsBurstDuplicate(2000, 1000)
                && EnemySpawnTrapRules.IsBurstDuplicate(2500, 1999),
            "only the first request in a continuously repeated one-second burst is accepted"
        );

        Assert(
            EnemySpawnTrapRules.SuppressRepeatedRoomClear(true, true)
                && !EnemySpawnTrapRules.SuppressRepeatedRoomClear(true, false)
                && !EnemySpawnTrapRules.SuppressRepeatedRoomClear(false, true)
                && !EnemySpawnTrapRules.SuppressRepeatedRoomClear(false, false),
            "only known traps after the first room clear suppress the last-enemy event"
        );

        Assert(
            !EnemySpawnTrapRules.HasActiveEncounter(true, true)
                && !EnemySpawnTrapRules.HasActiveEncounter(false, true)
                && !EnemySpawnTrapRules.HasActiveEncounter(false, false)
                && EnemySpawnTrapRules.HasActiveEncounter(true, false),
            "ending screens reject a retained encounter while valid rooms and the next run remain eligible"
        );

        Console.WriteLine("PASS: enemy trap strongest-enemy selection and forward placement rules");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception("Enemy spawn trap rules: " + message);
    }
}
