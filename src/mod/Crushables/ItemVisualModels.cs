using UntilYouFall.Shared;

namespace UntilYouFallArchipelago;

internal static class ItemVisualModels
{
    private const string Rewards = "Crushables/Rewards/";
    private const string Crushables = "Crushables/";

    private static readonly Dictionary<string, string> Exact = new()
    {
        [ApItems.ProgressiveWeaponUpgradeLevelName] = Crushables + "Armament_Upgrade_Visuals_HQ",
        [ApItems.ComboLimitBonusName] = Rewards + "Reward_ComboLength_HQ",
        [ApItems.CriticalWindowBonusName] = Rewards + "Reward_ComboLength_HQ",
        [ApItems.DashImpactBonusName] = Rewards + "Reward_DashDamage_HQ",
        [ApItems.DashChargesBonusName] = Rewards + "Reward_DashDamage_HQ",
        [ApItems.DashRechargeBonusName] = Rewards + "Reward_DashDamage_HQ",
        [ApItems.MaximumHealthBonusName] = Rewards + "Reward_MaxHealth_HQ",
        [ApItems.EnhanceSuperBonusName] = Rewards + "Reward_SuperUpgrade_HQ",
        [ApItems.AetherFillerName] = Rewards + "Reward_Aether_Lvl01_HQ",
        [ApItems.Aether500Name] = Rewards + "Reward_Aether_Lvl03_HQ",
        [ApItems.Aether750Name] = Rewards + "Reward_Aether_Lvl04_HQ",
        [ApItems.Aether1000Name] = Rewards + "Reward_Aether_Lvl05_HQ",
        [ApItems.ProgressiveAetherMultiplierName] = Rewards + "Reward_Aether_Lvl05_HQ",
        [ApItems.ShardOfVitalityName] = Rewards + "Reward_FullHeal_HQ",
        [ApItems.SuperChargeName] = Crushables + "Training_SuperCharge_Visuals_HQ",
        [$"Difficulty: {DifficultySettings.Names[0]}"] = Crushables + "Difficulty_Easy_Visuals_HQ",
        [$"Difficulty: {DifficultySettings.Names[1]}"] = Crushables + "Difficulty_Medium_Visuals_HQ",
        [$"Difficulty: {DifficultySettings.Names[2]}"] = Crushables + "Difficulty_Hard_Visuals_HQ",
        [$"Difficulty: {DifficultySettings.Names[3]}"] = Crushables + "Difficulty_Nightmare01_Visuals_HQ",
    };

    public static string? Resource(string game, string itemName)
    {
        if (game != GameIdentity.Name)
            return null;
        if (WeaponCatalog.All.Any(weapon => weapon.Name == itemName))
            return Crushables + "Armament_Purchase_Visuals_HQ";
        if (WeaponAbilityCatalog.ItemIds.ContainsKey(itemName))
            return Crushables + "Training_SuperCharge_Visuals_HQ";

        if (
            itemName.StartsWith("Extra Trait Slot: ", StringComparison.Ordinal)
            || itemName.StartsWith("Starting Trait: ", StringComparison.Ordinal)
            || itemName is ApItems.ExtraStartingTraitPickName or ApItems.ExtraStartingTraitOptionName
        )
            return Rewards + "Reward_Trait_HQ";
        if (itemName.EndsWith(" Health Damage +1", StringComparison.Ordinal))
            return Rewards + "Reward_Tradeoff_CritDamage_HQ";
        if (itemName.EndsWith(" Guard Damage +1", StringComparison.Ordinal))
            return Rewards + "Reward_Tradeoff_GuardDamage_HQ";

        return Exact.TryGetValue(itemName, out var resource) ? resource : null;
    }
}
