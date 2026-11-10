using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using UntilYouFall.Launcher.Core;

namespace UntilYouFall.Launcher.Views;

public partial class InstallView : UserControl
{
    private LauncherContext _context = null!;
    private bool _installing;
    private int _statusRefresh;
    private string? _savedGameDir;

    public InstallView() => InitializeComponent();

    public bool IsInstalling => _installing;

    internal void Initialize(LauncherContext context)
    {
        _context = context;
        LinuxGuide.IsVisible = OperatingSystem.IsLinux();
        LaunchOption.Text = LinuxSupport.LaunchOption;
        context.GameDirectoryText = () => GameLocator.Normalize(GameDir.Text);
        context.GetGameDirectory = GetGameDirectory;
        var savedDir = LauncherSettings.LoadGameDir();
        GameDir.Text = GameLocator.IsGameDir(savedDir) ? savedDir : GameLocator.Find() ?? savedDir ?? "";
    }

    public void AppendLog(string message) => Log.Text += message + Environment.NewLine;

    internal Task RefreshAsync() => RunUiAsync(RefreshStatusAsync);

    private string GetGameDirectory()
    {
        var dir = _context.GameDirectoryText();
        if (!GameLocator.IsGameDir(dir))
            throw new InvalidOperationException("Choose game install folder (the one containing UntilYouFall.exe)");

        return dir;
    }

    private async void Browse_Click(object? sender, RoutedEventArgs args) =>
        await RunUiAsync(async () =>
        {
            var folders = await TopLevel
                .GetTopLevel(this)!
                .StorageProvider.OpenFolderPickerAsync(
                    new FolderPickerOpenOptions { Title = "Select the game install folder" }
                );
            var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
            if (path == null)
                return;
            GameDir.Text = GameLocator.Normalize(path);
            await RefreshStatusAsync();
        });

    private async void Detect_Click(object? sender, RoutedEventArgs args) =>
        await RunUiAsync(async () =>
        {
            GameDir.Text = GameLocator.Find() ?? GameDir.Text;
            await RefreshStatusAsync();
        });

    private async void GameDir_LostFocus(object? sender, RoutedEventArgs args)
    {
        GameDir.Text = GameLocator.Normalize(GameDir.Text);
        await RefreshAsync();
    }

    private async void CopyLaunchOption_Click(object? sender, RoutedEventArgs args)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null)
            return;

        await clipboard.SetTextAsync(LinuxSupport.LaunchOption);
        _context.Log("Launch option copied");
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs args) => await RefreshAsync();

    private async void Install_Click(object? sender, RoutedEventArgs args) =>
        await RunInstallationAsync(
            "Install",
            async () =>
            {
                var dir = GetGameDirectory();
                var melonInstalled = MelonLoaderInstaller.IsInstalled(dir);
                if (!melonInstalled)
                    await MelonLoaderInstaller.InstallAsync(dir, _context.Lifetime);
                var updated = await Task.Run(() => ModInstaller.Install(dir, _context.Payload), _context.Lifetime);
                var icons = await SteamIconInstaller.InstallAsync(dir, cancel: _context.Lifetime);

                return (melonInstalled ? "" : "MelonLoader " + MelonLoaderInstaller.Version + " installed, ")
                    + $"{updated} mod files updated"
                    + (icons.Downloaded > 0 ? $", {icons.Downloaded} required assets downloaded" : "")
                    + (
                        icons.Failed > 0
                            ? $", {icons.Failed} required assets could not be downloaded (try again with internet access)"
                            : ""
                    );
            }
        );

    private async void Uninstall_Click(object? sender, RoutedEventArgs args) =>
        await RunInstallationAsync(
            "Uninstall",
            async () =>
            {
                var dir = GetGameDirectory();

                var removed = await Task.Run(() => ModInstaller.Uninstall(dir, _context.Payload), _context.Lifetime);
                SteamIconInstaller.Uninstall(dir);

                return $"{removed} files removed";
            }
        );

    private async Task RefreshStatusAsync()
    {
        var refresh = ++_statusRefresh;
        Files.ItemsSource = null;
        Ready.Text = "";
        var dir = _context.GameDirectoryText();
        await _context.RefreshVersions();
        if (refresh != _statusRefresh || dir != _context.GameDirectoryText())
            return;
        if (!GameLocator.IsGameDir(dir))
        {
            SetStatus(MelonStatus, "Game install folder not found", false);
            SetStatus(ModStatus, "", true);

            return;
        }
        if (_savedGameDir != dir)
        {
            if (LauncherSettings.SaveGameDir(dir))
                _savedGameDir = dir;
            else
                _context.Log(
                    "The game install folder could not be remembered, check write access to the launcher appdata folder"
                );
        }
        var melon = MelonLoaderInstaller.IsInstalled(dir);
        SetStatus(
            MelonStatus,
            melon
                ? "MelonLoader installed"
                    + (MelonLoaderInstaller.AssembliesGenerated(dir) ? "" : " (the first game start takes longer)")
                : "MelonLoader missing",
            melon
        );
        var entries = await Task.Run(() => ModInstaller.Status(dir, _context.Payload), _context.Lifetime);
        if (refresh != _statusRefresh || dir != _context.GameDirectoryText())
            return;
        Files.ItemsSource = entries;
        var current = entries.Count(entry => entry.State == FileState.Current);
        SetStatus(
            ModStatus,
            entries.Count == 0
                ? "Launcher package has no mod files"
                : $"Mod files: {current}/{entries.Count} up to date",
            entries.Count > 0 && current == entries.Count
        );
        if (melon && entries.Count > 0 && current == entries.Count)
            Ready.Text = OperatingSystem.IsLinux()
                ? "Game prepared - set the launch option, then launch the game from Steam"
                : "Game prepared - Launch game from Steam";
    }

    private static void SetStatus(TextBlock label, string text, bool success)
    {
        label.Text = text;
        label.Foreground = (IBrush)Application.Current!.FindResource(success ? "SuccessBrush" : "ErrorBrush")!;
    }

    private async Task RunInstallationAsync(string name, Func<Task<string>> action)
    {
        if (_installing)
            return;
        _installing = true;
        Actions.IsEnabled = false;
        try
        {
            await RunUiAsync(async () =>
            {
                _context.Log(name + ": working...");
                _context.Log(name + ": " + await action());
            });
            await RunUiAsync(RefreshStatusAsync);
        }
        finally
        {
            _installing = false;
            Actions.IsEnabled = true;
        }
    }

    private async Task RunUiAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException) when (_context.Lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _context.Log($"Failed ({ex.GetType().Name}): {ex.Message}");
        }
    }
}
