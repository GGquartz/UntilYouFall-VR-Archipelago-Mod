namespace UntilYouFallArchipelago;

internal enum StartingHands
{
    Both,
    LeftOnly,
    RightOnly,
}

internal static class StartingHandsRules
{
    public static StartingHands Read(IDictionary<string, object> slotData)
    {
        if (!slotData.TryGetValue("starting_hands", out var raw))
            throw new InvalidDataException("Missing starting hands in AP slot data");

        return raw?.ToString() switch
        {
            "both" => StartingHands.Both,
            "left_only" => StartingHands.LeftOnly,
            "right_only" => StartingHands.RightOnly,
            _ => throw new InvalidDataException("Unknown starting hands in AP slot data"),
        };
    }
}
