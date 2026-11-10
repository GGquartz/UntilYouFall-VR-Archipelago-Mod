using System.Reflection;
using System.Text.Json;
using UntilYouFall.Shared;
using UntilYouFallArchipelago;

internal static class SharedCatalogTests
{
    public static void Run()
    {
        Assert(
            GameIdentity.Name == "Until You Fall" && ArchipelagoClient.Game == GameIdentity.Name,
            "AP login keeps the manifest game identity"
        );
        using var items = Read("Items");
        foreach (var item in items.RootElement.GetProperty("Items").EnumerateArray())
        {
            var key = item.GetProperty("Key").GetString()!;
            var id = item.GetProperty("Id").GetInt64();
            Assert(
                (long)typeof(ApItems).GetField(key + "Id")!.GetRawConstantValue()! == id,
                key + " uses its catalog ID"
            );
            Assert(
                (string)typeof(ApItems).GetField(key + "Name")!.GetRawConstantValue()!
                    == item.GetProperty("Name").GetString(),
                key + " uses its catalog name"
            );
            if (item.TryGetProperty("Group", out var group) && group.GetString() == "PlayerBonus")
                Assert(
                    PlayerBonusRules.MaxCount(id) == item.GetProperty("Copies").GetInt32(),
                    key + " uses its catalog copy limit"
                );
            if (group.ValueKind == JsonValueKind.String && group.GetString() == "AetherBundle")
                Assert(
                    ApItems.AetherBundleAmount(id) == item.GetProperty("Amount").GetInt32(),
                    key + " uses its catalog Aether amount"
                );
            if (group.ValueKind == JsonValueKind.String && group.GetString() == "StartingTraitUpgrade")
                Assert(
                    StartingTraitRules.MaxCount(id) == item.GetProperty("Copies").GetInt32(),
                    key + " uses its catalog trait upgrade limit"
                );
        }

        using var weapons = Read("Weapons");
        var roster = weapons.RootElement.EnumerateArray().ToArray();
        Assert(WeaponCatalog.All.Length == roster.Length, "the weapon roster has one entry per weapons.json weapon");
        Assert(
            WeaponCatalog.All.Count(weapon => StartingWeaponRules.Eligible(weapon.Id))
                == roster.Count(weapon =>
                    !(weapon.TryGetProperty("SecondaryOnly", out var secondary) && secondary.GetBoolean())
                ),
            "starting weapon eligibility excludes exactly the secondary-only weapons"
        );

        using var difficulties = Read("Difficulties");
        var tier = 0;
        foreach (var difficulty in difficulties.RootElement.EnumerateArray())
        {
            Assert(
                DifficultySettings.Names[tier] == difficulty.GetProperty("Name").GetString()
                    && DifficultySettings.ItemId(tier) == difficulty.GetProperty("Id").GetInt64(),
                "difficulty names and IDs follow catalog order"
            );
            tier++;
        }

        using var abilities = Read("WeaponAbilities");
        var abilityIds = abilities
            .RootElement.EnumerateArray()
            .ToDictionary(
                ability => ability.GetProperty("Name").GetString()!,
                ability => ability.GetProperty("Id").GetInt64()
            );
        foreach (var ability in abilityIds)
        {
            Assert(ApItems.IsWeaponAbility(ability.Value), "ability membership uses the ability catalog");
            Assert(
                ItemVisualModels.Resource("Until You Fall", ability.Key)!.EndsWith("Training_SuperCharge_Visuals_HQ"),
                "ability visuals use the ability catalog"
            );
        }
        Assert(!ApItems.IsWeaponAbility(-1), "unknown ability IDs are not registered");
        foreach (var weapon in WeaponCatalog.All)
        {
            Assert(weapon.SuperItemId == abilityIds[weapon.Super], "weapons reference their shared ability ID");
            if (weapon.NoDamageItems)
                continue;
            Assert(
                PlayerBonusRules.MaxCount(weapon.DamageItemId)
                    == items.RootElement.GetProperty("DamageItemCopies").GetInt32()
                    && PlayerBonusRules.MaxCount(weapon.DamageItemId + 1)
                        == items.RootElement.GetProperty("DamageItemCopies").GetInt32(),
                "weapon damage items use the shared copy limit"
            );
        }

        using var locations = Read("Locations");
        var layout = locations.RootElement;
        Assert(StageChecks.Count == layout.GetProperty("StageCount").GetInt32(), "stage count comes from the catalog");
        for (var stage = 1; stage <= StageChecks.Count; stage++)
        {
            for (var weaponIndex = 0; weaponIndex < WeaponCatalog.All.Length; weaponIndex++)
                Assert(
                    StageChecks.LocationId(stage, WeaponCatalog.All[weaponIndex].Id)
                        == layout.GetProperty("StageWeaponBaseId").GetInt64()
                            + (stage - 1) * WeaponCatalog.All.Length
                            + weaponIndex
                            + 1,
                    "stage weapon ID uses the shared base and weapon order"
                );
            for (tier = 1; tier < DifficultySettings.Names.Length; tier++)
                Assert(
                    StageDifficultyChecks.LocationId(stage, tier)
                        == layout.GetProperty("StageDifficultyBaseId").GetInt64()
                            + (tier - 1) * StageChecks.Count
                            + stage,
                    "stage difficulty ID uses the shared base"
                );
        }
        for (var section = 1; section <= ShopChecks.Sections; section++)
        {
            Assert(
                ShopChecks.Price(section) == layout.GetProperty("ShopPrices")[section - 1].GetInt32(),
                "shop price comes from the catalog"
            );
            for (var slot = 1; slot <= ShopChecks.SlotsPerSection; slot++)
                Assert(
                    ShopChecks.LocationId(section, slot)
                        == layout.GetProperty("ShopBaseId").GetInt64()
                            + (section - 1) * layout.GetProperty("ShopSlotsPerSection").GetInt32()
                            + slot,
                    "shop location ID uses the shared layout"
                );
        }
        for (var index = 1; index <= StageRewardChecks.MaximumCount; index++)
            Assert(
                StageRewardChecks.LocationId(index) == layout.GetProperty("StageRewardBaseId").GetInt64() + index,
                "stage reward ID uses the shared base"
            );
        for (var index = 0; index < QuestChecks.All.Length; index++)
        for (var reward = 1; reward <= QuestChecks.All[index].Tier; reward++)
            Assert(
                QuestChecks.LocationId(index, reward)
                    == layout.GetProperty("QuestBaseId").GetInt64()
                        + index * layout.GetProperty("QuestRewardStride").GetInt32()
                        + reward,
                "quest reward ID uses the shared stride"
            );
        foreach (var enemy in EnemyChecks.All)
            Assert(
                EnemySpawnTrapRules.IsExcludedEnemy(enemy.Id) == enemy.ExcludeFromSpawnTrap,
                "trap exclusion follows the enemy catalog"
            );
        Assert(!EnemySpawnTrapRules.IsExcludedEnemy(-1), "unknown enemy IDs remain non-excluded");

        using var ranges = Read("OptionRanges");
        foreach (var option in ranges.RootElement.EnumerateObject())
        foreach (var limit in option.Value.EnumerateObject())
            Assert(
                (int)typeof(OptionRanges).GetField(option.Name + limit.Name)!.GetRawConstantValue()!
                    == limit.Value.GetInt32(),
                "client option limits and defaults match their named catalog fields"
            );
        Console.WriteLine("PASS shared catalog contracts");
    }

    private static JsonDocument Read(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"UntilYouFall.{name}.json")!;
        return JsonDocument.Parse(stream);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception("FAIL " + message);
    }
}
