namespace UntilYouFallArchipelago;

internal sealed partial record DifficultySettings(int Starting, int Maximum)
{
    public static bool IsItem(long id) => id >= FirstItemId && id < FirstItemId + Names.Length;

    public static long ItemId(int tier) =>
        tier >= 0 && tier < Names.Length ? FirstItemId + tier : throw new ArgumentOutOfRangeException(nameof(tier));

    public bool InRange(int tier) => tier <= Maximum && tier >= 0 && tier < Names.Length;

    public bool Allows(int tier, IReadOnlyCollection<long> owned) =>
        InRange(tier) && (tier == Starting || owned.Contains(ItemId(tier)));

    public static DifficultySettings Read(IDictionary<string, object> slotData)
    {
        var starting = ReadTier(slotData, "starting_difficulty");
        var maximum = ReadTier(slotData, "max_difficulty");
        if (starting > maximum)
            throw new InvalidDataException("Starting difficulty must not be harder than maximum difficulty");

        return new DifficultySettings(starting, maximum);
    }

    private static int ReadTier(IDictionary<string, object> slotData, string key)
    {
        if (!slotData.TryGetValue(key, out var raw) || raw is not (long or double))
            throw new InvalidDataException($"Missing or non numeric {key} in AP slot data");
        var value = Convert.ToDouble(raw);
        if (!double.IsFinite(value) || value < 0 || value >= Names.Length || value != Math.Truncate(value))
            throw new InvalidDataException(
                $"Invalid {key} expected a whole difficulty tier from 0 to {Names.Length - 1}"
            );

        return (int)value;
    }
}
