using Avalonia.Controls;
using UntilYouFall.Launcher.Core;

namespace UntilYouFall.Launcher;

public sealed partial class MainWindow : Window
{
    private readonly CancellationTokenSource _lifetime = new();

    public MainWindow()
    {
        InitializeComponent();
        LauncherVersion.Text = "v" + VersionInfo.LauncherVersion;
        var context = new LauncherContext(Path.Combine(AppContext.BaseDirectory, "payload"), _lifetime.Token, Log);
        HelpView.Initialize(context);
        context.RefreshVersions = HelpView.RefreshVersionsAsync;
        InstallView.Initialize(context);
        ConnectView.Initialize(context);
        OptionsView.Initialize(context);
        Opened += async (_, _) => await InstallView.RefreshAsync();
    }

    protected override void OnClosing(WindowClosingEventArgs args)
    {
        if (InstallView.IsInstalling)
        {
            args.Cancel = true;
            Log("Please wait for the process before closing the launcher");
            return;
        }
        ConnectView.SaveConnection();
        _lifetime.Cancel();
        base.OnClosing(args);
    }

    private void Log(string message)
    {
        InstallView.AppendLog($"{DateTime.Now:HH:mm:ss}  {message}");
        LogExporter.AppendLauncherLog($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}");
    }
}
