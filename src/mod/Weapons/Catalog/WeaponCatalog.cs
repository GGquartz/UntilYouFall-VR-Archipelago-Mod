using System.Reflection;
using System.Text.Json;

namespace UntilYouFallArchipelago;

internal sealed record WeaponItem(
    long Id,
    long TraitSlotItemId,
    string Super,
    string Name,
    string Guid,
    bool TwoHanded,
    bool LogicRequiresBothHands,
    bool SecondaryOnly = false,
    bool NoDamageItems = false,
    long DamageItemId = 0,
    string? VanillaHand = null
)
{
    public long SuperItemId => WeaponAbilityCatalog.ItemIds[Super];
}

internal static class WeaponCatalog
{
    public static readonly WeaponItem[] All = Load();

    public static WeaponItem? ById(long id) => All.FirstOrDefault(weapon => weapon.Id == id);

    public static WeaponItem VanillaStarter(bool left) =>
        All.First(weapon => weapon.VanillaHand == (left ? "Left" : "Right"));

    public static WeaponItem? ByTraitSlotId(long id) => All.FirstOrDefault(weapon => weapon.TraitSlotItemId == id);

    public static WeaponItem? BySuperItemId(long id) => All.FirstOrDefault(weapon => weapon.SuperItemId == id);

    public static WeaponItem? ByName(string name) => All.FirstOrDefault(weapon => weapon.Name == name);

    public static WeaponItem? ByGuid(string guid) =>
        All.FirstOrDefault(weapon => string.Equals(weapon.Guid, guid, StringComparison.OrdinalIgnoreCase));

    private static WeaponItem[] Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("UntilYouFall.Weapons.json")!;

        return JsonSerializer.Deserialize<WeaponItem[]>(stream)!;
    }
}
