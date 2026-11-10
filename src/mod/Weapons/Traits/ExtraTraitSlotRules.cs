namespace UntilYouFallArchipelago;

internal static class ExtraTraitSlotRules
{
    public static int Apply(int nativeSockets, long weaponId, IReadOnlyCollection<long> unlocks)
    {
        var weapon = WeaponCatalog.ById(weaponId);

        return weapon != null && unlocks.Contains(weapon.TraitSlotItemId) ? nativeSockets + 1 : nativeSockets;
    }
}
