using UntilYouFallArchipelago;

internal static class ItemVisualModelsTests
{
    public static void Run()
    {
        foreach (var weapon in WeaponCatalog.All)
        {
            Check("Until You Fall", weapon.Name, "Crushables/Armament_Purchase_Visuals_HQ");
            Check("Until You Fall", weapon.Super, "Crushables/Training_SuperCharge_Visuals_HQ");
        }
        var original = WeaponCatalog.All[0];
        try
        {
            WeaponCatalog.All[0] = original with { Name = "Catalog Test Weapon", Super = "Catalog Test Super" };
            Check("Until You Fall", "Catalog Test Weapon", "Crushables/Armament_Purchase_Visuals_HQ");
            Check("Until You Fall", "Catalog Test Super", null);
            Check("Until You Fall", original.Super, "Crushables/Training_SuperCharge_Visuals_HQ");
            Check("Other Game", "Catalog Test Weapon", null);
            Check("Other Game", "Catalog Test Super", null);
        }
        finally
        {
            WeaponCatalog.All[0] = original;
        }
        Check("Until You Fall", "Mistcutter", "Crushables/Armament_Purchase_Visuals_HQ");
        Check("Until You Fall", "Extra Trait Slot: Heartsplitter", "Crushables/Rewards/Reward_Trait_HQ");
        foreach (var trait in StartingTraitRules.All)
        {
            Check("Until You Fall", $"Starting Trait: {trait.Name}", "Crushables/Rewards/Reward_Trait_HQ");
            Check("Other Game", $"Starting Trait: {trait.Name}", null);
        }
        Check("Until You Fall", "Extra Starting Trait Pick", "Crushables/Rewards/Reward_Trait_HQ");
        Check("Until You Fall", "Extra Starting Trait Option", "Crushables/Rewards/Reward_Trait_HQ");
        Check("Other Game", "Extra Starting Trait Pick", null);
        Check("Other Game", "Extra Starting Trait Option", null);
        Check("Until You Fall", "Ritual Dagger Health Damage +1", "Crushables/Rewards/Reward_Tradeoff_CritDamage_HQ");
        Check("Until You Fall", "Ritual Dagger Guard Damage +1", "Crushables/Rewards/Reward_Tradeoff_GuardDamage_HQ");
        Check("Until You Fall", "Player Bonus: Dash Charges +1", "Crushables/Rewards/Reward_DashDamage_HQ");
        Check(
            "Until You Fall",
            "Player Bonus: +10% Critical Strike Window Length",
            "Crushables/Rewards/Reward_ComboLength_HQ"
        );
        Check("Until You Fall", "Player Bonus: -10% Dash Recharge Time", "Crushables/Rewards/Reward_DashDamage_HQ");
        Check("Other Game", "Player Bonus: +10% Critical Strike Window Length", null);
        Check("Other Game", "Player Bonus: -10% Dash Recharge Time", null);
        Check("Until You Fall", "Progressive Aether Multiplier", "Crushables/Rewards/Reward_Aether_Lvl05_HQ");
        Check("Until You Fall", "Difficulty: Challenging", "Crushables/Difficulty_Medium_Visuals_HQ");
        Check("Until You Fall", "Difficulty: Relaxed", "Crushables/Difficulty_Easy_Visuals_HQ");
        Check("Until You Fall", "Difficulty: Relentless", "Crushables/Difficulty_Hard_Visuals_HQ");
        Check("Until You Fall", "Difficulty: Nightmare", "Crushables/Difficulty_Nightmare01_Visuals_HQ");
        Check("Until You Fall", "Dodge Only Trap", null);
        Check("Until You Fall", "Progressive Shop Unlock", null);
        Check("Other Game", "Mistcutter", null);
        Console.WriteLine("PASS: item visual model mapping for own game and foreign items");
    }

    private static void Check(string game, string item, string? expected)
    {
        var actual = ItemVisualModels.Resource(game, item);
        if (actual != expected)
            throw new Exception($"{item}: expected {expected ?? "null"}, got {actual ?? "null"}");
    }
}
