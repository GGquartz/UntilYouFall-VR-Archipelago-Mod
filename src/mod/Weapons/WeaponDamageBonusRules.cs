using System.Globalization;

namespace UntilYouFallArchipelago;

internal enum WeaponDamageKind
{
    Health,
    Guard,
}

internal static class WeaponDamageBonusRules
{
    public static readonly long[] ItemIds = WeaponCatalog
        .All.Where(weapon => !weapon.NoDamageItems)
        .SelectMany(weapon => new[] { weapon.DamageItemId, weapon.DamageItemId + 1 })
        .ToArray();

    public static long ItemId(long weaponId, WeaponDamageKind kind) =>
        WeaponCatalog.ById(weaponId)!.DamageItemId + (kind == WeaponDamageKind.Guard ? 1 : 0);

    public static bool IsItem(long id) => ItemIds.Contains(id);

    public static string Display(float nativeValue, int count, bool sharedDamage = false) =>
        nativeValue.ToString("0.##", CultureInfo.InvariantCulture)
        + (sharedDamage ? "*" : "")
        + (count > 0 ? $"{(sharedDamage ? "" : " ")}{ApColors.Rich(ApColors.ModAccent, $"+{count}")}" : "");
}
