namespace UntilYouFallArchipelago;

internal sealed class ClientSlotSettings
{
    public long[] StartingWeapons { get; }
    public StartingHands StartingHands { get; }
    public bool SeparateWeaponSupers { get; }
    public int StageRewardCheckCount { get; }
    public int TrapEffectLengthSeconds { get; }
    public DifficultySettings Difficulty { get; }
    public DeathLinkMode DeathLinkMode { get; }
    public int[] ActiveQuests { get; }
    public int QuestHintCost { get; }
    public GoalSettings Goal { get; }
    public bool WeaponHintsEnabled { get; }

    private ClientSlotSettings(IDictionary<string, object> slotData)
    {
        StartingWeapons = StartingWeaponRules.Read(slotData);
        StartingHands = StartingHandsRules.Read(slotData);
        SeparateWeaponSupers = WeaponAbilityRules.ReadSeparateItems(slotData);
        StageRewardCheckCount = StageRewardChecks.ReadCount(slotData);
        TrapEffectLengthSeconds = TimedTrapState.ReadDuration(slotData);
        Difficulty = DifficultySettings.Read(slotData);
        DeathLinkMode = DeathLinkRules.Read(slotData);
        ActiveQuests = QuestChecks.ReadActive(slotData);
        QuestHintCost = QuestChecks.ReadHintCost(slotData);
        Goal = GoalRules.Read(slotData);
        WeaponHintsEnabled = slotData.TryGetValue("weapon_hints", out var hints) && Convert.ToBoolean(hints);
    }

    public static ClientSlotSettings Read(IDictionary<string, object> slotData) => new(slotData);
}
