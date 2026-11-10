using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using UntilYouFall.Launcher.Core;

namespace UntilYouFall.Launcher.ViewModels;

internal sealed record Choice(string Value, string Label)
{
    public override string ToString() => Label;
}

internal enum OptionKind
{
    Choice,
    Range,
    Toggle,
    List,
    Items,
    Weights,
}

internal sealed class OptionViewModel : INotifyPropertyChanged
{
    private static readonly Dictionary<string, string> LabelOverrides = new()
    {
        ["deaths_door"] = "Death's Door",
        ["first_25_percent_of_checks"] = "First 25% of checks",
        ["first_50_percent_of_checks"] = "First 50% of checks",
        ["spellswords_charm"] = "Spellsword's Charm",
        ["knight_blooded_crest"] = "Knight-Blooded Crest",
        ["high_arcanists_blade"] = "High Arcanist's Blade",
        ["fates_end"] = "Fate's End",
        ["captains_warhammer"] = "Captain's Warhammer",
        ["vestals_wrath"] = "Vestal's Wrath",
        ["duelists_edge"] = "Duelist's Edge",
    };

    private Choice? _selectedChoice;
    private double _number;
    private bool _flag;
    private string _text = "";

    private OptionViewModel(string key, string label, string help, OptionKind kind, string defaultValue)
    {
        Key = key;
        Label = label;
        Help = help;
        Kind = kind;
        DefaultValue = defaultValue;
        Choices = [];
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Changed;

    public string Key { get; }
    public string Label { get; }
    public string Help { get; }
    public OptionKind Kind { get; }
    public string DefaultValue { get; }
    public IReadOnlyList<Choice> Choices { get; private init; }
    public double Min { get; private init; }
    public double Max { get; private init; }

    public bool IsChoice => Kind == OptionKind.Choice;
    public bool IsRange => Kind == OptionKind.Range;
    public bool IsSlider => IsRange && Max - Min <= 100;
    public bool IsNumberBox => IsRange && Max - Min > 100;
    public bool IsToggle => Kind == OptionKind.Toggle;
    public bool IsList => Kind == OptionKind.List;
    public bool IsItems => Kind == OptionKind.Items;
    public bool IsWeights => Kind == OptionKind.Weights;
    public ObservableCollection<WeightRow> Weights { get; } = [];
    private Dictionary<string, int> _weightDefaults = [];
    private Dictionary<string, int> _itemDefaults = [];

    public ObservableCollection<SelectedItem> Items { get; } = [];
    public IReadOnlyList<ItemInfo> Catalog { get; private init; } = [];
    public bool Counted { get; private init; }
    private static readonly string[] TierNames = ["Easy", "Medium", "Hard"];

    public IReadOnlyDictionary<string, int> QuestLimitsByTier { get; private init; } = new Dictionary<string, int>();
    public bool HasTiers => QuestLimitsByTier.Count > 0;
    public string Summary =>
        string.Join(
            "  -  ",
            QuestLimitsByTier.Select(tier => $"{tier.Key} {Items.Count(item => item.Tag == tier.Key)}/{tier.Value}")
        );
    public IEnumerable<string> Suggestions => Catalog.Select(SuggestionText);

    private string SuggestionText(ItemInfo item) => HasTiers ? $"{item.Name} ({item.Class})" : item.Name;

    public Choice? SelectedChoice
    {
        get => _selectedChoice;
        set => Set(ref _selectedChoice, value);
    }
    public double Number
    {
        get => _number;
        set => Set(ref _number, Math.Round(value));
    }
    public bool Flag
    {
        get => _flag;
        set => Set(ref _flag, value);
    }
    public string Text
    {
        get => _text;
        set => Set(ref _text, value ?? "");
    }

    public string Value =>
        Kind switch
        {
            OptionKind.Choice => SelectedChoice?.Value ?? DefaultValue,
            OptionKind.Range => Number.ToString(CultureInfo.InvariantCulture),
            OptionKind.Toggle => Flag ? "true" : "false",
            OptionKind.Items => string.Join(", ", Items.Select(item => item.Name)),
            OptionKind.Weights => OptionsCatalog.YamlMap(Weights.ToDictionary(row => row.Name, row => row.Weight)),
            _ => Text,
        };

    public string YamlValue =>
        Kind switch
        {
            OptionKind.List => OptionsCatalog.YamlList(Text),
            OptionKind.Items => Counted
                ? OptionsCatalog.YamlMap(Items.ToDictionary(item => item.Name, item => item.Count))
                : OptionsCatalog.YamlNames(Items.Select(item => item.Name)),
            _ => Value,
        };

    public void AddItem(string name)
    {
        var info = Catalog.FirstOrDefault(item => item.Name == name || SuggestionText(item) == name);
        if (info == null)
            return;
        var existing = Items.FirstOrDefault(item => item.Name == info.Name);
        if (existing == null)
            Hook(
                new SelectedItem(info.Name, Counted ? info.MaxCopies : 1, Counted, HasTiers ? info.Class : ""),
                add: true
            );
        else
            existing.Count++;
    }

    public void RemoveItem(SelectedItem item) => Hook(item, add: false);

    private void Hook(SelectedItem item, bool add)
    {
        if (add)
        {
            Items.Add(item);
            item.PropertyChanged += OnItemChanged;
        }
        else
        {
            Items.Remove(item);
            item.PropertyChanged -= OnItemChanged;
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
        Changed?.Invoke();
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs args) => Changed?.Invoke();

    public static OptionViewModel From(OptionDefinition option, IReadOnlyList<ItemInfo>? items = null)
    {
        var kind = option.Type switch
        {
            "choice" => OptionKind.Choice,
            "range" => OptionKind.Range,
            "toggle" => OptionKind.Toggle,
            "set" or "items" or "inventory" => OptionKind.Items,
            "list" => OptionKind.List,
            "weights" => OptionKind.Weights,
            _ => throw new InvalidOperationException("Invalid option type: " + option.Type),
        };
        var catalog = option.Type is "items" or "inventory"
            ? items ?? ItemCatalog.Load()
            : option.Values.Select(value => new ItemInfo(value.Name, TierNames[value.Tier - 1], 1, true)).ToList();
        var label = option.Key == "start_inventory_from_pool" ? "Starting Items" : option.Display;
        var model = new OptionViewModel(option.Key, label, option.Help, kind, option.DefaultText)
        {
            Choices = option.Choices.Select(choice => new Choice(choice, ChoiceLabel(choice))).ToList(),
            Min = option.Min,
            Max = option.Max,
            Catalog = option.Type == "inventory" ? catalog.Where(item => item.CanStartWith).ToList() : catalog,
            Counted = option.Type == "inventory",
            QuestLimitsByTier = option.TierLimits.ToDictionary(
                limit => TierNames[int.Parse(limit.Key) - 1],
                limit => limit.Value
            ),
        };
        if (kind == OptionKind.Weights)
        {
            model._weightDefaults = option
                .Default.EnumerateObject()
                .ToDictionary(entry => entry.Name, entry => entry.Value.GetInt32());
            foreach (var key in option.Keys)
            {
                var row = new WeightRow(key);
                row.WeightChanged += model.OnWeightChanged;
                model.Weights.Add(row);
            }
        }
        if (option.Type == "items")
            model._itemDefaults = option.Default.EnumerateArray().ToDictionary(entry => entry.GetString()!, entry => 1);
        else if (option.Type == "inventory")
            model._itemDefaults = option
                .Default.EnumerateObject()
                .ToDictionary(entry => entry.Name, entry => entry.Value.GetInt32());
        model.Reset();

        return model;
    }

    private void OnWeightChanged()
    {
        var total = Weights.Sum(row => row.Weight);
        foreach (var row in Weights)
            row.Total = total;
        Changed?.Invoke();
    }

    public static IEnumerable<OptionViewModel> General(IReadOnlyList<ItemInfo>? items = null)
    {
        items ??= ItemCatalog.Load();

        return OptionsCatalog.Load("general-options.json").Select(option => From(option, items));
    }

    public void Reset()
    {
        SelectedChoice = Choices.FirstOrDefault(choice => choice.Value == DefaultValue) ?? Choices.FirstOrDefault();
        Number = double.TryParse(DefaultValue, CultureInfo.InvariantCulture, out var number) ? number : 0;
        Flag = DefaultValue == "true";
        Text = IsList ? DefaultValue : "";
        foreach (var row in Weights)
            row.Weight = _weightDefaults.GetValueOrDefault(row.Name);
        foreach (var item in Items.ToList())
            RemoveItem(item);
        foreach (var (name, count) in _itemDefaults)
        {
            AddItem(name);
            if (Items.FirstOrDefault(item => item.Name == name) is { } selected)
                selected.Count = count;
        }
    }

    private static string ChoiceLabel(string choice) =>
        LabelOverrides.TryGetValue(choice, out var label)
            ? label
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(choice.Replace('_', ' '));

    private void Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        Changed?.Invoke();
    }
}
