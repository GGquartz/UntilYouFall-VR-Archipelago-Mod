using UntilYouFallArchipelago;

internal static class PlayerBonusTests
{
    public static void Run()
    {
        var expected = new Dictionary<long, int>
        {
            [910440] = 5,
            [910441] = 3,
            [910442] = 3,
            [910443] = 3,
            [910444] = 1,
            [910445] = 3,
            [910446] = 3,
        };
        Assert(PlayerBonusRules.ItemIds.SequenceEqual(expected.Keys), "stable IDs stay contiguous and ordered");
        Assert(
            Enumerable.Range(910500, 28).All(id => PlayerBonusRules.MaxCount(id) == 3 && ApItems.IsPlayerBonus(id)),
            "all 28 weapon damage IDs use durable capped receipt counts"
        );
        Assert(
            !ApItems.IsPlayerBonus(910499) && !ApItems.IsPlayerBonus(910528),
            "weapon damage range excludes adjacent IDs and Charm"
        );
        Assert(
            WeaponDamageBonusRules.ItemIds.SequenceEqual(Enumerable.Range(910500, 28).Select(id => (long)id)),
            "weapon damage IDs follow catalog order without Charm"
        );
        Assert(
            WeaponDamageBonusRules.ItemId(910019, WeaponDamageKind.Guard) == 910517
                && WeaponDamageBonusRules.ItemId(910014, WeaponDamageKind.Health) == 910506,
            "Mistcutter guard and Ritual Dagger health match APWorld IDs"
        );
        foreach (var weapon in WeaponCatalog.All.Where(weapon => !weapon.NoDamageItems))
            Assert(
                WeaponDamageBonusRules.ItemId(weapon.Id, WeaponDamageKind.Health) == weapon.DamageItemId
                    && WeaponDamageBonusRules.ItemId(weapon.Id, WeaponDamageKind.Guard) == weapon.DamageItemId + 1,
                $"{weapon.Name} uses its catalog damage IDs for both stats"
            );
        Assert(
            PlayerBonusRules.ReceiptItemIds.Length == 35
                && PlayerBonusRules.Clamp(910506, 99) == 3
                && PlayerBonusRules.Clamp(910506, -1) == 0,
            "receipt replay includes weapon bonuses and clamps to three"
        );
        Assert(
            WeaponDamageBonusRules.Display(7, 1) == "7 <color=#A6E6FF>+1</color>"
                && WeaponDamageBonusRules.Display(11, 3) == "11 <color=#A6E6FF>+3</color>"
                && WeaponDamageBonusRules.Display(7.5f, 0) == "7.5",
            "only the compact AP suffix is light blue; zero bonus has no suffix"
        );
        Assert(
            WeaponDamageBonusRules.Display(12, 3, sharedDamage: true) == "12*<color=#A6E6FF>+3</color>"
                && WeaponDamageBonusRules.Display(12, 0, sharedDamage: true) == "12*"
                && WeaponDamageBonusRules.Display(12, 0) == "12",
            "native shared-damage star replaces the separator without coloring the native value or star"
        );
        Assert(
            expected.All(entry => PlayerBonusRules.MaxCount(entry.Key) == entry.Value),
            "pool counts match the approved 5/3/3/3/1/3/3 distribution"
        );
        Assert(
            expected.Keys.All(ApItems.IsPlayerBonus) && !ApItems.IsPlayerBonus(910439),
            "only player-bonus IDs use the permanent count path"
        );
        Assert(
            PlayerBonusRules.Clamp(910440, 99) == 5 && PlayerBonusRules.Clamp(910444, 2) == 1,
            "counts are capped per item"
        );
        Assert(
            PlayerBonusRules.Amount(910442, 3) == 15 && PlayerBonusRules.Amount(910441, 2) == 2,
            "dash impact scales by five while additive stats scale by one"
        );
        Assert(
            PlayerBonusRules.Label(910440, 2) == "Maximum Health +2"
                && PlayerBonusRules.Label(910442, 3) == "Dash Impact +15"
                && PlayerBonusRules.Label(910444, 1) == "Super Enhanced",
            "inspection labels show total permanent bonuses"
        );
        foreach (var copies in Enumerable.Range(0, 4))
        {
            Assert(
                Math.Abs(PlayerBonusRules.CriticalWindowMultiplier(copies) - (1f + copies * 0.1f)) < 0.0001f
                    && Math.Abs(PlayerBonusRules.DashRechargeTimeMultiplier(copies) - (1f - copies * 0.1f)) < 0.0001f,
                "timing copies add ten percentage points rather than compounding"
            );
            Assert(
                Math.Abs(4f + PlayerBonusRules.DashRechargeDelta(4f, copies, 0) - (4f - copies * 0.4f)) < 0.0001f,
                "native four-second dash interval becomes 3.6, 3.2 and 2.8 seconds"
            );
        }
        Assert(
            PlayerBonusRules.CriticalWindowMultiplier(-1) == 1f
                && Math.Abs(PlayerBonusRules.CriticalWindowMultiplier(99) - 1.3f) < 0.0001f
                && Math.Abs(PlayerBonusRules.DashRechargeTimeMultiplier(99) - 0.7f) < 0.0001f
                && PlayerBonusRules.DashRechargeTimeMultiplier(-1) == 1f,
            "timing effects clamp invalid and excess counts"
        );
        Assert(
            PlayerBonusRules.DashRechargeDelta(4f, 3, 3) == 0f
                && Math.Abs(PlayerBonusRules.DashRechargeDelta(4f, 3, 2) + 0.4f) < 0.0001f
                && Math.Abs(PlayerBonusRules.DashRechargeDelta(4f, 0, 3) - 1.2f) < 0.0001f,
            "same scene owner is not reapplied and history reset removes only the AP contribution"
        );
        Assert(
            PlayerBonusRules.Label(910445, 1) == "Critical Strike Window +10%"
                && PlayerBonusRules.Label(910445, 3) == "Critical Strike Window +30%"
                && PlayerBonusRules.Label(910446, 2) == "Dash Recharge Time -20%"
                && PlayerBonusRules.Label(910445, 0) == null
                && PlayerBonusRules.Label(910446, 0) == null,
            "inspection percentages show the total owned bonus"
        );
        var right = new object();
        var left = new object();
        Assert(!PlayerBonusRules.PresentHands<object>(null, null).Any(), "no equipped hands produce no targets");
        Assert(
            PlayerBonusRules.PresentHands(right, null).SequenceEqual(new[] { right }),
            "right-only loadouts keep the right target"
        );
        Assert(
            PlayerBonusRules.PresentHands(null, left).SequenceEqual(new[] { left }),
            "left-only loadouts keep the left target"
        );
        Assert(
            PlayerBonusRules.PresentHands(right, left).SequenceEqual(new[] { right, left }),
            "two-handed loadouts keep both targets in stable order"
        );
        Assert(
            PlayerBonusRules.IsNativeBonusRow("StatBuffTemplate")
                && PlayerBonusRules.IsNativeBonusRow("StatBuffTemplate (9)")
                && !PlayerBonusRules.IsNativeBonusRow("AP Player Bonus Row 1"),
            "inspection discovery includes only native weapon-bonus rows"
        );
        Assert(
            PlayerBonusRules.PlayerHeaderPosition(-62f, -202.5f, 90f) == -354.5f
                && PlayerBonusRules.PlayerHeaderPosition(-62f, -302.5f, 90f) == -454.5f,
            "player header starts at the actual bottom edge of the final weapon-bonus row"
        );
        Assert(
            !PlayerBonusRules.CanApplyBonuses(false) && PlayerBonusRules.CanApplyBonuses(true),
            "all permanent bonuses wait for the PlayerHealth start lifecycle"
        );
        Assert(
            PlayerBonusRules.RemainingApplications(4, 0) == 4
                && PlayerBonusRules.RemainingApplications(4, 3) == 1
                && PlayerBonusRules.RemainingApplications(4, 4) == 0,
            "scene replay applies only the missing permanent count"
        );
        Assert(
            PlayerBonusRules.AllowedMaxHealthApplications(10f, 5f, 1f, 5) == 5
                && PlayerBonusRules.AllowedMaxHealthApplications(10f, 6f, 1f, 5) == 4
                && PlayerBonusRules.AllowedMaxHealthApplications(10f, 10f, 1f, 3) == 0
                && PlayerBonusRules.AllowedMaxHealthApplications(10f, 11f, 1f, 3) == 0,
            "maximum health bonus never pushes the total above the native pip capacity"
        );
        Assert(
            PlayerBonusRules.CanEnhanceSuper(0, 3)
                && PlayerBonusRules.CanEnhanceSuper(1, 3)
                && !PlayerBonusRules.CanEnhanceSuper(2, 3)
                && !PlayerBonusRules.CanEnhanceSuper(3, 3),
            "the Player Bonus leaves the top super level to a reward"
        );
        Assert(
            !PlayerBonusRules.IsSuperMaxed(2, 3)
                && PlayerBonusRules.IsSuperMaxed(3, 3)
                && !PlayerBonusRules.IsSuperMaxed(0, 0),
            "a reward is blocked only once the super is on its highest level (III of IV stays upgradeable)"
        );

        var directory = Path.Combine(Path.GetTempPath(), "uyf-player-bonus-" + Guid.NewGuid());
        try
        {
            var state = ApState.Open(directory, "server/seed-A/team0/slot1");
            state.SetPlayerBonusItemCount(910440, 3);
            state.SetPlayerBonusItemCount(910442, 99);
            state.SetPlayerBonusItemCount(910440, 1);
            state.SetPlayerBonusItemCount(910506, 2);
            state.SetPlayerBonusItemCount(910517, 99);
            state.SetPlayerBonusItemCount(910445, 2);
            state.SetPlayerBonusItemCount(910446, 99);
            var reloaded = ApState.Open(directory, "server/seed-A/team0/slot1");
            Assert(
                reloaded.PlayerBonusItemCount(910440) == 3 && reloaded.PlayerBonusItemCount(910442) == 3,
                "counts persist monotonically and clamp to their pool maximum"
            );
            Assert(
                reloaded.PlayerBonusItemCount(910506) == 2 && reloaded.PlayerBonusItemCount(910517) == 3,
                "per-weapon damage counts survive reload independently with capped copies"
            );
            Assert(
                reloaded.PlayerBonusItemCount(910445) == 2 && reloaded.PlayerBonusItemCount(910446) == 3,
                "timing bonuses use the same durable capped ownership as other player bonuses"
            );
            Assert(
                ApState.Open(directory, "server/seed-B/team0/slot1").PlayerBonusItemCounts.Count == 0,
                "counts are isolated by seed and slot"
            );
            reloaded.ResetItemHistory();
            Assert(
                ApState.Open(directory, "server/seed-A/team0/slot1").PlayerBonusItemCounts.Count == 0,
                "server history reset clears permanent player bonuses"
            );
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

        Console.WriteLine("PASS: player-bonus IDs, counts, values, hands, labels and durable ownership");
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok)
            throw new Exception("Player bonuses: " + message);
    }
}
