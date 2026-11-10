using UntilYouFallArchipelago;

internal static class EnemyChecksTests
{
    public static void Run()
    {
        var names = new[]
        {
            "Lesser Husk",
            "Unraveled Husk",
            "Rokari Knight",
            "Rokari Captain",
            "Rokari Spellweaver",
            "Empowered Knight",
            "Rokari Honor Guard",
            "Empowered Captain",
            "Empowered Spellweaver",
            "Aether Horror",
        };
        Assert(
            EnemyChecks
                .All.Select(enemy => enemy.LocationName)
                .SequenceEqual(names.Select(name => "First Kill: " + name)),
            "enemy locations match the requested roster"
        );
        Assert(
            EnemyChecks.All.Select(enemy => enemy.Id).SequenceEqual(Enumerable.Range(910301, 10).Select(id => (long)id))
                && ArchipelagoClient.BaseCheckIds.Distinct().Count() == 325,
            "enemy IDs are stable and do not collide with stage IDs"
        );
        Assert(
            EnemyChecks.ByGuid("52d24c14814c28c4f8c8c5fe17f3ec9c")?.Id == 910310
                && EnemyChecks.ByGuid("4a1bd840d43c3ad4dbf480d1682bf784")?.Id == 910310,
            "normal and empowered Aether Horrors share one first-kill check"
        );
        Assert(
            EnemyChecks.ByGuid("c2ced8b8b930c9944b1160b8aa7e1124") == null
                && EnemyChecks.ByGuid("9117c3bf84ef7924eb36e3e4a5f2d6b6") == null,
            "cinematic boss and training dummy are excluded"
        );
        Assert(
            EnemyChecks.ByGuid("B0036F00D8D07D341BAA59E3AA561F6E")?.Id == 910303
                && EnemyChecks.ByGuid("dd344cc397e68d145867653177114959")?.Id == 910306,
            "GUID matching ignores casing but distinguishes empowered knights"
        );
        Assert(
            EnemyChecks.ByGuid(null) == null && EnemyChecks.ByGuid("unknown") == null,
            "unknown definitions never guess an enemy type"
        );
        Assert(Eligible(), "depleted enemy in a live ordinary run can award a check");
        Assert(
            !Eligible(campaign: "b16668e6a16418848ab46f22c8817b9c") && !Eligible(campaign: null),
            "tutorial and missing campaign cannot award kills"
        );
        Assert(
            !Eligible(stage: -1) && !Eligible(stage: 21) && !Eligible(loading: true),
            "hub, out-of-campaign stages and loading cleanup are excluded"
        );
        Assert(
            !Eligible(alive: false) && !Eligible(dying: true) && !Eligible(health: 1) && !Eligible(health: float.NaN),
            "player death, repeated death, live enemy and invalid health are excluded"
        );
        var directory = Path.Combine(Path.GetTempPath(), "uyf-enemy-checks-" + Guid.NewGuid());
        try
        {
            var state = ApState.Open(directory, "seed-A/slot1");
            state.RecordCheck(910310);
            state.RecordCheck(910310);
            Assert(
                ApState.Open(directory, "seed-A/slot1").Checks.SetEquals(new[] { 910310L }),
                "boss variants/repeated kills persist a single check across restarts"
            );
            Assert(
                ApState.Open(directory, "seed-B/slot1").Checks.Count == 0
                    && ApState.Open(directory, "seed-A/slot2").Checks.Count == 0,
                "new seed/slot resets enemy first kills"
            );
        }
        finally
        {
            Directory.Delete(directory, true);
        }
        Console.WriteLine(
            "PASS: enemy identities, boss variants, kill eligibility and persistent first-kill isolation"
        );
    }

    private static bool Eligible(
        string? campaign = StageChecks.CampaignGuid,
        int stage = 6,
        bool loading = false,
        bool alive = true,
        bool dying = false,
        float health = 0
    ) => EnemyChecks.IsCombatKill(campaign, stage, loading, alive, dying, health);

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
