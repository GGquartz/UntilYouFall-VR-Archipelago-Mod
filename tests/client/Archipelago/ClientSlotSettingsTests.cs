using UntilYouFallArchipelago;

internal static class ClientSlotSettingsTests
{
    public static void Run()
    {
        var data = ValidSlotData();
        var settings = ClientSlotSettings.Read(data);
        Check(settings.StartingWeapons.SequenceEqual(StartingWeaponRules.Read(data)), "starting weapons");
        Check(settings.StartingHands == StartingHands.LeftOnly, "starting hands");
        Check(settings.SeparateWeaponSupers, "separate weapon supers");
        Check(settings.StageRewardCheckCount == 12, "stage rewards");
        Check(settings.TrapEffectLengthSeconds == 45, "trap duration");
        Check(settings.Difficulty.Starting == 1 && settings.Difficulty.Maximum == 3, "difficulty range");
        Check(settings.DeathLinkMode == DeathLinkMode.DeathsDoor, "death link mode");
        Check(settings.ActiveQuests.SequenceEqual(new[] { 0, 1, 2 }), "active quests");
        Check(settings.QuestHintCost == 1500, "quest hint cost");
        Check(settings.Goal == new GoalSettings(GoalMode.RunsMaxDifficulty, 2), "goal");
        Check(!settings.WeaponHintsEnabled, "missing weapon hints retains the disabled default");
        data["weapon_hints"] = true;
        Check(ClientSlotSettings.Read(data).WeaponHintsEnabled, "weapon hints enabled");
        data.Remove("quest_hint_cost");
        Check(ClientSlotSettings.Read(data).QuestHintCost == QuestChecks.DefaultHintCost, "default hint cost");

        Reject("starting_weapons", new[] { "unknown weapon" }, slot => StartingWeaponRules.Read(slot));
        Reject("starting_hands", "unknown", slot => StartingHandsRules.Read(slot));
        Reject("weapon_supers", "unknown", slot => WeaponAbilityRules.ReadSeparateItems(slot));
        Reject("stage_reward_checks", -1, slot => StageRewardChecks.ReadCount(slot));
        Reject("trap_effect_length", -1, slot => TimedTrapState.ReadDuration(slot));
        Reject("max_difficulty", 0L, slot => DifficultySettings.Read(slot));
        Reject("death_link_mode", "unknown", slot => DeathLinkRules.Read(slot));
        Reject("quests", new[] { -1 }, slot => QuestChecks.ReadActive(slot));
        Reject("quest_hint_cost", -1, slot => QuestChecks.ReadHintCost(slot));
        Reject("goal", "unknown", slot => GoalRules.Read(slot));
        Reject("weapon_hints", "unknown", slot => Convert.ToBoolean(slot["weapon_hints"]));

        Console.WriteLine("PASS: client slot settings reuse validators, values, defaults and errors");
    }

    internal static Dictionary<string, object> ValidSlotData() =>
        new()
        {
            ["starting_weapons"] = new[]
            {
                WeaponCatalog.All.First(weapon => StartingWeaponRules.Eligible(weapon.Id)).Name,
            },
            ["starting_hands"] = "left_only",
            ["weapon_supers"] = "separate_items",
            ["stage_reward_checks"] = 12L,
            ["trap_effect_length"] = 45L,
            ["starting_difficulty"] = 1L,
            ["max_difficulty"] = 3L,
            ["death_link"] = true,
            ["death_link_mode"] = "deaths_door",
            ["quests"] = new[] { 0, 1, 2 },
            ["quest_hint_cost"] = 1500L,
            ["goal"] = "runs_max_difficulty",
            ["goal_runs"] = 2L,
        };

    private static void Reject(string key, object value, Action<IDictionary<string, object>> validator)
    {
        var data = ValidSlotData();
        data[key] = value;
        var expected = Failure(() => validator(data));
        var actual = Failure(() => ClientSlotSettings.Read(data));
        Check(actual.GetType() == expected.GetType() && actual.Message == expected.Message, key + " error unchanged");
    }

    private static Exception Failure(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            return exception;
        }
        throw new Exception("Invalid slot settings were accepted");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception("Client slot settings: " + message);
    }
}
