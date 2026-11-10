using System.Reflection;
using System.Text.Json;

namespace UntilYouFall.Launcher.Core;

internal sealed record ItemInfo(string Name, string Class, int MaxCopies, bool CanStartWith);

internal static class ItemCatalog
{
    public static List<ItemInfo> Load()
    {
        using var stream =
            Assembly.GetExecutingAssembly().GetManifestResourceStream("items.json")
            ?? throw new InvalidOperationException("items.json is not embedded");

        return JsonSerializer.Deserialize<List<ItemInfo>>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? throw new InvalidDataException("items.json is empty");
    }
}
