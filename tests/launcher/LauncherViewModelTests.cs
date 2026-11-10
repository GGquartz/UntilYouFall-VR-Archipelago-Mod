using UntilYouFall.Launcher.Core;
using UntilYouFall.Launcher.ViewModels;

internal static class LauncherViewModelTests
{
    public static void Run(Action<string, bool> check)
    {
        var definitions = OptionsCatalog.Load();
        var models = definitions.Select(option => OptionViewModel.From(option)).ToList();
        var defaults = OptionsCatalog.Defaults(definitions);

        check("one view model per catalog option", models.Count == definitions.Count);
        check("view model defaults match catalog", models.All(model => model.Value == defaults[model.Key]));

        foreach (var model in models.Where(model => model.IsChoice))
        {
            var definition = definitions.Single(option => option.Key == model.Key);
            check(
                "choices keep catalog order " + model.Key,
                model.Choices.Select(choice => choice.Value).SequenceEqual(definition.Choices)
            );
            foreach (var choice in model.Choices)
            {
                model.SelectedChoice = choice;
                check("choice maps to YAML " + model.Key + "=" + choice.Value, model.Value == choice.Value);
            }
        }

        var early = models.Single(model => model.Key == "early_hand");
        check(
            "early hand labels",
            early
                .Choices.Select(choice => choice.Label)
                .SequenceEqual(new[] { "First 25% of checks", "First 50% of checks", "Unrestricted" })
        );
        check(
            "death's door label",
            models.Single(model => model.Key == "death_link_mode").Choices.Any(choice => choice.Label == "Death's Door")
        );
        check(
            "weapon labels keep apostrophes",
            models
                .Single(model => model.Key == "second_starting_weapon")
                .Choices.Any(choice => choice.Label == "Captain's Warhammer")
        );

        var range = models.Single(model => model.Key == "quest_hint_cost");
        range.Number = 1200;
        check(
            "range value reaches YAML",
            OptionsCatalog
                .BuildYaml("Player", models.ToDictionary(m => m.Key, m => m.Value), definitions)
                .Contains("  quest_hint_cost: 1200\n")
        );
        var changes = 0;
        range.Changed += () => changes++;
        range.Number = 1300;
        check("changes are reported", changes == 1);

        var toggle = models.Single(model => model.Key == "quest_board");
        toggle.Flag = false;
        check("toggle maps to false", toggle.Value == "false");

        foreach (var model in models)
            model.Reset();
        check("reset restores defaults", models.All(model => model.Value == defaults[model.Key]));

        var general = OptionViewModel.General().ToList();
        check(
            "general options first values",
            general
                .Take(2)
                .Select(model => model.Key + "=" + model.YamlValue)
                .SequenceEqual(new[] { "accessibility=full", "progression_balancing=50" })
        );
        check(
            "all supported common options share one catalog",
            general
                .Select(model => model.Key)
                .SequenceEqual(
                    new[]
                    {
                        "accessibility",
                        "progression_balancing",
                        "local_items",
                        "non_local_items",
                        "start_inventory_from_pool",
                        "start_hints",
                        "start_location_hints",
                        "priority_locations",
                        "exclude_locations",
                    }
                )
        );
        var generalDefinitions = OptionsCatalog.Load("general-options.json");
        check(
            "general view models keep source metadata",
            general.All(model =>
            {
                var definition = generalDefinitions.Single(option => option.Key == model.Key);
                return model.Help == definition.Help && model.DefaultValue == definition.DefaultText;
            })
        );
        check(
            "general defaults serialize to standard YAML",
            general
                .Skip(2)
                .Select(model => model.YamlValue)
                .SequenceEqual(new[] { "[]", "[]", "{}", "[]", "[]", "[]", "[]" })
        );
        check(
            "accessibility description matches Archipelago",
            general.Single(model => model.Key == "accessibility").Help
                == "Set rules for reachability of your items/locations.\n\n**Full:** ensure everything can be reached and acquired.\n\n**Minimal:** ensure what is needed to reach your goal can be acquired."
        );
        check(
            "progression balancing description matches Archipelago",
            general.Single(model => model.Key == "progression_balancing").Help
                == "A system that can move progression earlier, to try and prevent the player from getting stuck and bored early.\n\nA lower setting means more getting stuck. A higher setting means less getting stuck."
        );
        var common = general.Skip(2).ToList();
        check(
            "common descriptions match Archipelago",
            common[0].Help == "Forces these items to be in their native world."
                && common[1].Help == "Forces these items to be outside their native world."
        );
        check(
            "item pickers keep the complete catalog",
            common
                .Where(model => model.Key is "local_items" or "non_local_items" or "start_hints")
                .All(model => model.IsItems && !model.Counted && model.Catalog.SequenceEqual(ItemCatalog.Load()))
        );
        common[0].AddItem("Shard of Vitality");
        common[0].AddItem("Shard of Vitality");
        check(
            "picked names become a YAML list once",
            common[0].YamlValue == "[\"Shard of Vitality\"]" && common[1].YamlValue == "[]"
        );

        var start = common.Single(model => model.Key == "start_inventory_from_pool");
        start.AddItem("Player Bonus: Maximum Health +1");
        start.AddItem("Player Bonus: Maximum Health +1");
        start.AddItem("Ritual Dagger");
        start.AddItem("Shard of Vitality");
        start.AddItem("Elite Encounter Trap");
        start.AddItem("Mistcutter Health Damage +1");
        check(
            "filler and traps are not startable",
            start.Items.Count == 3 && start.Items.All(i => i.Name != "Shard of Vitality")
        );
        start.Items.Remove(start.Items.Last());
        start.AddItem("Left Hand");
        check("hands are not startable", start.Items.Count == 2);
        check(
            "start inventory counts copies",
            start.YamlValue == "{\"Player Bonus: Maximum Health +1\": 2, \"Ritual Dagger\": 1}"
        );
        start.Items[0].Count = 99;
        check("count is capped at pool size", start.Items[0].Count == 5);
        var priority = general.Single(model => model.Key == "priority_locations");
        priority.Text = "Shop: Test, Stage: Test";
        check(
            "location options remain comma-separated fields",
            general
                .Where(model => model.Key is "start_location_hints" or "priority_locations" or "exclude_locations")
                .All(model => model.IsList)
                && priority.YamlValue == "[\"Shop: Test\", \"Stage: Test\"]"
        );
        var yaml = OptionsCatalog.BuildYaml(
            "Player",
            defaults,
            definitions,
            general.ToDictionary(model => model.Key, model => model.YamlValue)
        );
        check(
            "general options appear once in YAML",
            general.All(model => yaml.Split('\n').Count(line => line.StartsWith("  " + model.Key + ": ")) == 1)
        );
        check(
            "counted starting items reach YAML",
            yaml.Contains(
                "  start_inventory_from_pool: {\"Player Bonus: Maximum Health +1\": 5, \"Ritual Dagger\": 1}\n"
            )
        );
        start.Reset();
        check("reset clears items", start.YamlValue == "{}");
        foreach (var model in general)
            model.Reset();
        check(
            "general reset restores source defaults",
            general
                .Skip(2)
                .Select(model => model.YamlValue)
                .SequenceEqual(new[] { "[]", "[]", "{}", "[]", "[]", "[]", "[]" })
        );

        using var defaultJson = new MemoryStream(
            System.Text.Encoding.UTF8.GetBytes(
                """
                [
                  {"key":"local_items","type":"items","default":["Ritual Dagger"]},
                  {"key":"start_inventory_from_pool","type":"inventory","default":{"Player Bonus: Maximum Health +1":2}},
                  {"key":"priority_locations","type":"list","default":["Shop: Test"]}
                ]
                """
            )
        );
        var defaultModels = OptionsCatalog.Parse(defaultJson).Select(option => OptionViewModel.From(option)).ToList();
        var expectedDefaults = new[]
        {
            "[\"Ritual Dagger\"]",
            "{\"Player Bonus: Maximum Health +1\": 2}",
            "[\"Shop: Test\"]",
        };
        check(
            "nonempty general defaults are applied",
            defaultModels.Select(model => model.YamlValue).SequenceEqual(expectedDefaults)
        );
        defaultModels[0].AddItem("Mistcutter");
        defaultModels[1].Items[0].Count = 4;
        defaultModels[2].Text = "Other location";
        foreach (var model in defaultModels)
            model.Reset();
        check(
            "nonempty general defaults survive reset",
            defaultModels.Select(model => model.YamlValue).SequenceEqual(expectedDefaults)
        );
    }
}
