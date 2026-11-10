using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using UntilYouFall.Launcher.Core;
using UntilYouFall.Launcher.ViewModels;

namespace UntilYouFall.Launcher.Views;

public partial class OptionsView : UserControl
{
    private LauncherContext _context = null!;
    private List<OptionDefinition> _definitions = [];
    private List<OptionViewModel> _gameOptions = [];
    private List<OptionViewModel> _extraOptions = [];

    public OptionsView() => InitializeComponent();

    internal void Initialize(LauncherContext context)
    {
        _context = context;
        _definitions = OptionsCatalog.Load();
        _gameOptions = _definitions.Select(option => OptionViewModel.From(option)).ToList();
        _extraOptions = OptionViewModel.General().ToList();
        var general = _extraOptions.Take(2);
        var common = _extraOptions.Skip(2);
        OptionList.ItemsSource = general.Concat(_gameOptions).Concat(common).ToList();
        foreach (var option in _gameOptions.Concat(_extraOptions))
            option.Changed += () => Validate();
        Validate();
    }

    private Dictionary<string, string> GameValues() =>
        _gameOptions.ToDictionary(option => option.Key, option => option.Value);

    private Dictionary<string, string> GameYamlValues() =>
        _gameOptions.ToDictionary(option => option.Key, option => option.IsItems ? option.YamlValue : option.Value);

    private bool Validate()
    {
        var problem = OptionsCatalog.Validate(
            PlayerName.Text ?? "",
            GameValues(),
            _definitions,
            _extraOptions
                .Where(option => option.Key == "start_inventory_from_pool")
                .SelectMany(option => option.Items)
                .Select(item => item.Name),
            _gameOptions
                .Where(option => option.IsItems)
                .ToDictionary(
                    option => option.Key,
                    option => (IReadOnlyList<string>)option.Items.Select(item => item.Name).ToList()
                )
        );
        Validation.Text = problem ?? "";

        return problem == null;
    }

    private void PlayerName_TextChanged(object? sender, TextChangedEventArgs args)
    {
        if (_definitions.Count > 0)
            Validate();
    }

    private void Reset_Click(object? sender, RoutedEventArgs args)
    {
        foreach (var option in _gameOptions.Concat(_extraOptions))
            option.Reset();
    }

    private async void SaveYaml_Click(object? sender, RoutedEventArgs args)
    {
        if (!Validate())
            return;

        try
        {
            var name = PlayerName.Text ?? "";
            var file = await TopLevel
                .GetTopLevel(this)!
                .StorageProvider.SaveFilePickerAsync(
                    new FilePickerSaveOptions
                    {
                        Title = "Save player YAML",
                        SuggestedFileName = name + ".yaml",
                        DefaultExtension = "yaml",
                        FileTypeChoices = [new FilePickerFileType("YAML") { Patterns = ["*.yaml"] }],
                    }
                );
            var path = file?.TryGetLocalPath();
            if (path == null)
                return;
            var extra = _extraOptions.ToDictionary(option => option.Key, option => option.YamlValue);
            File.WriteAllText(path, OptionsCatalog.BuildYaml(name, GameYamlValues(), _definitions, extra));
            _context.Log("Saved " + path);
        }
        catch (Exception error)
        {
            _context.Log("Failed: " + error.Message);
        }
    }
}
