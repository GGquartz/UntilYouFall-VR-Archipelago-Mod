namespace UntilYouFallArchipelago;

internal enum DeathLinkMode
{
    Off,
    Hard,
    Soft,
    Trap,
    DeathsDoor,
}

internal static class DeathLinkRules
{
    public static DeathLinkMode Read(IDictionary<string, object> slotData)
    {
        if (
            !slotData.TryGetValue("death_link", out var enabled)
            || enabled == null
            || !slotData.TryGetValue("death_link_mode", out var mode)
            || mode == null
        )
            throw new InvalidDataException("Missing death link settings in AP slot data");
        if (!Convert.ToBoolean(enabled))
            return DeathLinkMode.Off;

        return Convert.ToString(mode) switch
        {
            "hard" => DeathLinkMode.Hard,
            "soft" => DeathLinkMode.Soft,
            "trap" => DeathLinkMode.Trap,
            "deaths_door" => DeathLinkMode.DeathsDoor,
            var other => throw new InvalidDataException($"Unsupported death link mode '{other}' in AP slot data"),
        };
    }
}
