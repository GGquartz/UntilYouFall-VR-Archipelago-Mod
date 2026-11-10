using System.Reflection;
using System.Text.Json;

namespace UntilYouFallArchipelago;

internal static class WeaponAbilityCatalog
{
    private sealed record AbilityItem(long Id, string Name);

    public static readonly IReadOnlyDictionary<string, long> ItemIds = Load();

    private static IReadOnlyDictionary<string, long> Load()
    {
        using var stream = Assembly
            .GetExecutingAssembly()
            .GetManifestResourceStream("UntilYouFall.WeaponAbilities.json")!;

        return JsonSerializer
            .Deserialize<AbilityItem[]>(stream)!
            .ToDictionary(ability => ability.Name, ability => ability.Id);
    }
}
