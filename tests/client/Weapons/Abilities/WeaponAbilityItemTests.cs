using UntilYouFallArchipelago;

internal static class WeaponAbilityItemTests
{
    public static void Run()
    {
        var expected = new Dictionary<string, long>
        {
            ["Bulwark"] = 910430,
            ["Concussive Blast"] = 910431,
            ["Empowered Strike"] = 910432,
            ["Shardstorm"] = 910433,
            ["Soul Burn"] = 910434,
            ["Stasis Strike"] = 910435,
            ["Titan's Strength"] = 910436,
            ["Vicious Strike"] = 910437,
            ["Void Trap"] = 910438,
        };
        Assert(
            WeaponCatalog.All.Select(weapon => weapon.SuperItemId).Distinct().Order().SequenceEqual(expected.Values),
            "catalog contains exactly the nine stable super item IDs"
        );
        Assert(
            WeaponCatalog.All.All(weapon => expected[weapon.Super] == weapon.SuperItemId),
            "every weapon maps to its named super item"
        );
        Assert(
            expected.Values.All(ApItems.IsWeaponAbility) && !ApItems.IsWeaponAbility(910420),
            "only super item IDs use the durable ability receipt path"
        );

        var mistcutter = WeaponCatalog.ById(910019)!;
        Assert(
            WeaponAbilityRules.Locked(true, mistcutter, Array.Empty<long>()),
            "separate mode locks a super without its item"
        );
        Assert(
            !WeaponAbilityRules.Locked(true, mistcutter, new[] { mistcutter.SuperItemId })
                && !WeaponAbilityRules.Locked(false, mistcutter, Array.Empty<long>()),
            "matching item or unlocked-with-weapon mode enables the super"
        );
        Assert(
            WeaponAbilityRules.ReadSeparateItems(
                new Dictionary<string, object> { ["weapon_supers"] = "separate_items" }
            )
                && !WeaponAbilityRules.ReadSeparateItems(
                    new Dictionary<string, object> { ["weapon_supers"] = "unlocked_with_weapon" }
                ),
            "slot data selects the requested super mode"
        );

        var directory = Path.Combine(Path.GetTempPath(), "uyf-ability-state-test-" + Guid.NewGuid());
        try
        {
            var state = ApState.Open(directory, "server/seed-A/team0/slot1");
            state.UnlockWeaponAbility(mistcutter.SuperItemId);
            state.UnlockWeaponAbility(mistcutter.SuperItemId);
            state.CommitItem(0);
            var reloaded = ApState.Open(directory, "server/seed-A/team0/slot1");
            Assert(
                reloaded.WeaponAbilityUnlocks.SetEquals(new[] { mistcutter.SuperItemId }) && reloaded.HandledCount == 1,
                "super ownership persists idempotently with the item cursor"
            );
            reloaded.ResetItemHistory();
            Assert(
                ApState.Open(directory, "server/seed-A/team0/slot1").WeaponAbilityUnlocks.Count == 0,
                "server history reset clears super ownership"
            );
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

        Console.WriteLine("PASS: weapon-super catalog, slot mode, lock rule and durable ownership");
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok)
            throw new Exception("Weapon ability items: " + message);
    }
}
