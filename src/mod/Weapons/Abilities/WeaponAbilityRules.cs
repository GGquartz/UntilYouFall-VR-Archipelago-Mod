namespace UntilYouFallArchipelago;

internal static class WeaponAbilityRules
{
    public static bool ReadSeparateItems(IDictionary<string, object> slotData)
    {
        if (!slotData.TryGetValue("weapon_supers", out var raw))
            throw new InvalidDataException("Missing weapon supers in AP slot data");

        return raw?.ToString() switch
        {
            "unlocked_with_weapon" => false,
            "separate_items" => true,
            _ => throw new InvalidDataException("Unknown weapon supers in AP slot data"),
        };
    }

    public static bool Locked(bool separateItems, WeaponItem? weapon, IReadOnlyCollection<long> unlocks) =>
        separateItems && weapon != null && !unlocks.Contains(weapon.SuperItemId);
}
