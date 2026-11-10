using System.Collections;

namespace UntilYouFallArchipelago;

internal static class StartingWeaponRules
{
    public static long[] Read(IDictionary<string, object> slotData)
    {
        if (!slotData.TryGetValue("starting_weapons", out var raw) || raw is string || raw is not IEnumerable values)
            throw new InvalidDataException("Missing starting weapon in AP slot data");

        var names = values.Cast<object>().Select(value => value?.ToString()).ToArray();
        var weapons = names.Select(name => WeaponCatalog.All.FirstOrDefault(item => item.Name == name)).ToArray();
        if (
            weapons.Length is < 1 or > 2
            || names.Distinct().Count() != names.Length
            || weapons.Any(weapon => weapon == null || !Eligible(weapon.Id))
        )
            throw new InvalidDataException("AP slot must have one or two eligible starting weapons");

        return weapons.Select(weapon => weapon!.Id).ToArray();
    }

    public static long? ForHand(IReadOnlyList<long> weapons, StartingHands hands, bool left, bool rightDominant)
    {
        if (weapons.Count == 0)
            return null;
        if (HandUnlockRules.ShouldCarryStarter(hands, left, rightDominant))
            return weapons[0];

        return hands == StartingHands.Both && weapons.Count == 2 ? weapons[1] : null;
    }

    public static bool Eligible(long id) => WeaponCatalog.ById(id) is { SecondaryOnly: false };
}
