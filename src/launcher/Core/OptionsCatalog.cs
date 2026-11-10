using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using UntilYouFall.Shared;

namespace UntilYouFall.Launcher.Core;

internal sealed record OptionValue(string Name, int Tier);

internal sealed class OptionDefinition
{
    public string Key { get; set; } = "";
    public string Display { get; set; } = "";
    public string Help { get; set; } = "";
    public string Type { get; set; } = "";
    public List<string> Choices { get; set; } = new();

    [JsonPropertyName("both_hands_choices")]
    public List<string> BothHandsChoices { get; set; } = new();
    public int Min { get; set; }
    public int Max { get; set; }
    public JsonElement Default { get; set; }
    public List<string> Keys { get; set; } = new();
    public List<OptionValue> Values { get; set; } = new();

    [JsonPropertyName("tier_limits")]
    public Dictionary<string, int> TierLimits { get; set; } = new();

    public string DefaultText =>
        Type switch
        {
            "toggle" => Default.GetBoolean() ? "true" : "false",
            "range" => Default.GetInt32().ToString(),
            "weights" or "inventory" => OptionsCatalog.YamlMap(
                Default.EnumerateObject().ToDictionary(entry => entry.Name, entry => entry.Value.GetInt32())
            ),
            "items" => OptionsCatalog.YamlNames(Default.EnumerateArray().Select(entry => entry.GetString()!)),
            "list" => string.Join(", ", Default.EnumerateArray().Select(entry => entry.GetString()!)),
            _ => Default.GetString() ?? "",
        };
}

internal static class OptionsCatalog
{
    public const string Game = GameIdentity.Name;

    public static List<OptionDefinition> Load(string resourceName = "options.json")
    {
        using var stream =
            Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"{resourceName} is not embedded");

        return Parse(stream);
    }

    public static List<OptionDefinition> Parse(Stream json) =>
        JsonSerializer.Deserialize<List<OptionDefinition>>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        ) ?? throw new InvalidDataException("options.json is empty");

    public static Dictionary<string, string> Defaults(IEnumerable<OptionDefinition> options) =>
        options.ToDictionary(option => option.Key, option => option.DefaultText);

    public static string? Validate(
        string playerName,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyList<OptionDefinition> options,
        IEnumerable<string>? startItems = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? sets = null
    )
    {
        if (playerName.Length is < 1 or > 16)
            return "Player name must be 1-16 characters";

        if (playerName.Any(char.IsControl) || playerName.Contains(':'))
            return "Player name contains invalid characters";

        if (
            values.GetValueOrDefault("starting_hands", "both") != "both"
            && values.GetValueOrDefault("starting_weapon") is { } startingWeapon
            && options.Any(option =>
                option.Key == "starting_weapon" && option.BothHandsChoices.Contains(startingWeapon)
            )
        )
            return "A two-handed starting weapon requires starting hands: both";

        if (
            values.TryGetValue("starting_weapon", out var first)
            && values.TryGetValue("second_starting_weapon", out var second)
            && first != "random"
            && first == second
        )
            return "Starting weapons must be different";

        if (
            values.GetValueOrDefault("trap_percent") is { } trapPercent
            && trapPercent != "0"
            && values.GetValueOrDefault("trap_weights") is { } weights
            && !Regex.IsMatch(weights, @": [1-9]")
        )
            return "Trap weights: at least one trap type needs a weight above 0";

        if (values.GetValueOrDefault("quest_board") == "true" && QuestProblem(options, sets) is { } questProblem)
            return questProblem;

        var order = options.FirstOrDefault(option => option.Key == "max_difficulty")?.Choices;
        if (order != null && values.TryGetValue("max_difficulty", out var limit))
            foreach (var item in startItems ?? [])
                if (
                    item.StartsWith("Difficulty: ")
                    && order.IndexOf(item["Difficulty: ".Length..].ToLowerInvariant()) > order.IndexOf(limit)
                )
                    return $"Starting difficulty \"{item}\" is harder than maximum difficulty";

        if (
            order == null
            || !values.TryGetValue("starting_difficulty", out var start)
            || !values.TryGetValue("max_difficulty", out var max)
        )
            return null;

        var maxIndex = order.IndexOf(max);
        if (start == "random")
            return maxIndex <= 0 ? "Starting difficulty random needs maximum difficulty above Relaxed" : null;

        return order.IndexOf(start) > maxIndex ? "Starting difficulty cannot be harder than maximum difficulty" : null;
    }

    private static readonly string[] TierNames = ["Easy", "Medium", "Hard"];

    private static string? QuestProblem(
        IReadOnlyList<OptionDefinition> options,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? sets
    )
    {
        if (sets == null)
            return null;

        foreach (var option in options.Where(option => option.Type == "set" && sets.ContainsKey(option.Key)))
        {
            var tiers = option.Values.ToDictionary(value => value.Name, value => value.Tier);
            for (var tier = 1; tier <= TierNames.Length; tier++)
            {
                var limit = option.TierLimits.GetValueOrDefault(tier.ToString());
                if (sets[option.Key].Count(name => tiers.GetValueOrDefault(name) == tier) > limit)
                    return $"{option.Display}: maximum {limit} {TierNames[tier - 1]} quests";
            }
        }

        if (sets.TryGetValue("excluded_quests", out var excluded))
            foreach (var option in options.Where(option => option.Type == "set" && option.Key == "required_quests"))
                for (var tier = 1; tier <= TierNames.Length; tier++)
                {
                    var total = option.Values.Count(value => value.Tier == tier);
                    var kept =
                        total - option.Values.Count(value => value.Tier == tier && excluded.Contains(value.Name));
                    var needed = option.TierLimits.GetValueOrDefault(tier.ToString());
                    if (kept < needed)
                        return $"Too many {TierNames[tier - 1]} quests excluded, at least {needed} of {total} must be available";
                }
        var both = sets.GetValueOrDefault("required_quests", [])
            .Intersect(sets.GetValueOrDefault("excluded_quests", []))
            .Order()
            .ToList();
        return both.Count > 0 ? "Quests cannot be required and excluded: " + string.Join(", ", both) + "." : null;
    }

    private static string YamlValue(OptionDefinition option, string value) =>
        option.Type == "set" && value == "" ? "[]" : value;

    public static string BuildYaml(
        string playerName,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyList<OptionDefinition> options,
        IReadOnlyDictionary<string, string>? common = null
    )
    {
        var yaml = new StringBuilder();
        yaml.Append("name: ").Append(Quote(playerName)).Append('\n');
        yaml.Append("game: ").Append(Game).Append('\n');
        yaml.Append("description: Generated by the Until You Fall Archipelago launcher\n");
        yaml.Append(Game).Append(":\n");
        foreach (var option in options)
            yaml.Append("  ")
                .Append(option.Key)
                .Append(": ")
                .Append(YamlValue(option, values.GetValueOrDefault(option.Key, option.DefaultText)))
                .Append('\n');
        foreach (var (key, value) in common ?? new Dictionary<string, string>())
            yaml.Append("  ").Append(key).Append(": ").Append(value).Append('\n');

        return yaml.ToString();
    }

    public static string YamlList(string text) =>
        "["
        + string.Join(", ", text.Split(',').Select(name => name.Trim()).Where(name => name.Length > 0).Select(Quote))
        + "]";

    public static string YamlNames(IEnumerable<string> names) => "[" + string.Join(", ", names.Select(Quote)) + "]";

    public static string YamlMap(IReadOnlyDictionary<string, int> counts) =>
        "{" + string.Join(", ", counts.Select(entry => Quote(entry.Key) + ": " + entry.Value)) + "}";

    private static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
