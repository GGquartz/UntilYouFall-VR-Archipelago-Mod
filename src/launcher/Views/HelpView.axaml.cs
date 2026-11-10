using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using UntilYouFall.Launcher.Core;

namespace UntilYouFall.Launcher.Views;

public partial class HelpView : UserControl
{
    private LauncherContext _context = null!;
    private int _refresh;

    public HelpView() => InitializeComponent();

    internal void Initialize(LauncherContext context) => _context = context;

    internal async Task RefreshVersionsAsync()
    {
        var refresh = ++_refresh;
        var dir = _context.GameDirectoryText();
        var entries = await Task.Run(() => VersionInfo.Read(dir, _context.Payload), _context.Lifetime);
        if (refresh != _refresh || dir != _context.GameDirectoryText())
            return;
        Versions.ItemsSource = entries;
    }

    private async void RefreshVersions_Click(object? sender, RoutedEventArgs args)
    {
        try
        {
            await RefreshVersionsAsync();
        }
        catch (OperationCanceledException) when (_context.Lifetime.IsCancellationRequested) { }
        catch (Exception error)
        {
            _context.Log($"Failed ({error.GetType().Name}): {error.Message}");
        }
    }

    private async void ExportLogs_Click(object? sender, RoutedEventArgs args)
    {
        try
        {
            var file = await TopLevel
                .GetTopLevel(this)!
                .StorageProvider.SaveFilePickerAsync(
                    new FilePickerSaveOptions
                    {
                        Title = "Export logs",
                        SuggestedFileName = $"UYF-Archipelago-logs-{DateTime.Now:yyyyMMdd-HHmm}.zip",
                        DefaultExtension = "zip",
                        FileTypeChoices = [new FilePickerFileType("Zip") { Patterns = ["*.zip"] }],
                    }
                );
            var path = file?.TryGetLocalPath();
            if (path == null)
                return;
            LogExporter.Export(
                _context.GetGameDirectory(),
                _context.Payload,
                path,
                LogExporter.LauncherLogPath,
                VersionInfo.LauncherVersion
            );
            _context.Log("Logs exported to " + path);
        }
        catch (Exception error)
        {
            _context.Log("Failed: " + error.Message);
        }
    }
}
