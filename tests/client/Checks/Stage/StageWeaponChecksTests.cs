using UntilYouFallArchipelago;

internal static class StageWeaponChecksTests
{
    public static void Run()
    {
        var expected = 911001L;
        foreach (var stage in Enumerable.Range(1, StageChecks.Count))
        foreach (var weapon in WeaponCatalog.All)
            Assert(
                StageChecks.LocationId(stage, weapon.Id) == expected++,
                "stage/weapon IDs are contiguous and stable"
            );
        Assert(expected == 911316, "all 315 stage/weapon IDs are covered");
        Assert(
            StageChecks.Name(7, "Mistcutter") == "Clear Stage 7 - Defeat Boss 1 with Mistcutter"
                && StageChecks.Name(14, "Mistcutter") == "Clear Stage 14 - Defeat Boss 2 with Mistcutter"
                && StageChecks.Name(21, "Mistcutter") == "Clear Stage 21 - Defeat Final Boss with Mistcutter",
            "boss location names retain their labels"
        );

        var first = WeaponCatalog.All[0];
        var second = WeaponCatalog.All[1];
        Assert(
            StageChecks
                .ResolveWeapons(new[] { first.Guid, second.Guid })
                .Select(weapon => weapon.Id)
                .SequenceEqual(new[] { first.Id, second.Id }),
            "two distinct equipped weapons resolve to two checks"
        );
        Assert(
            StageChecks.ResolveWeapons(new string?[] { first.Guid, first.Guid }).Count == 1,
            "the same weapon in both hand slots resolves once"
        );
        Assert(
            StageChecks.ResolveWeapons(new string?[] { null, "", "unknown" }).Count == 0,
            "empty and unknown hand slots are ignored"
        );
        Assert(
            WeaponCatalog.All.Count(weapon => weapon.TwoHanded) == 3
                && WeaponCatalog.All.Count(weapon => weapon.LogicRequiresBothHands) == 5,
            "client catalog reads hand metadata"
        );
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
