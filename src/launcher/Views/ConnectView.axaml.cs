using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using UntilYouFall.Launcher.Core;
using UntilYouFall.Shared;

namespace UntilYouFall.Launcher.Views;

public partial class ConnectView : UserControl
{
    private LauncherContext _context = null!;
    private bool _connecting;

    public ConnectView() => InitializeComponent();

    internal void Initialize(LauncherContext context)
    {
        _context = context;
        var saved = LauncherSettings.LoadConnection();
        if (saved == null)
            return;
        Server.Text = saved.Server;
        Slot.Text = saved.Slot;
    }

    public void SaveConnection()
    {
        if (
            !LauncherSettings.SaveConnection(new ConnectionSettings(Server.Text?.Trim() ?? "", Slot.Text?.Trim() ?? ""))
        )
            _context.Log("Failed to save connection settings");
    }

    private async void Connect_Click(object? sender, RoutedEventArgs args)
    {
        if (_connecting)
            return;

        _connecting = true;
        ConnectButton.IsEnabled = false;
        DisconnectButton.IsEnabled = false;
        try
        {
            SaveConnection();
            var dir = _context.GetGameDirectory();
            var server = Server.Text?.Trim() ?? "";
            var slot = Slot.Text?.Trim() ?? "";
            var password = Password.Text ?? "";
            LauncherProtocol.ConnectCommand(server, slot, password);
            if (ModInstaller.GameRunning())
            {
                GameLink.SendConnectCommand(dir, server, slot, password);
                Show("Connecting...", "SecondaryTextBrush");
                var (State, Detail) = await GameLink.WaitForStatusAsync(dir, _context.Lifetime);
                var ok = State == ConnectionState.Connected;
                Show(ok ? "Connected successfully" : "Error: " + Detail, ok ? "SuccessBrush" : "ErrorBrush");
            }
            else
            {
                Show("Start the game to connect to the server", "SecondaryTextBrush");
            }
        }
        catch (OperationCanceledException) when (_context.Lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Show($"Error ({ex.GetType().Name}): {ex.Message}", "ErrorBrush");
        }
        finally
        {
            _connecting = false;
            ConnectButton.IsEnabled = true;
            DisconnectButton.IsEnabled = true;
        }
    }

    private async void Disconnect_Click(object? sender, RoutedEventArgs args)
    {
        if (_connecting)
            return;

        _connecting = true;
        ConnectButton.IsEnabled = false;
        DisconnectButton.IsEnabled = false;
        try
        {
            if (!ModInstaller.GameRunning())
            {
                Show("Game is not running", "SecondaryTextBrush");
                return;
            }
            var dir = _context.GetGameDirectory();
            GameLink.SendDisconnectCommand(dir);
            Show("Disconnecting...", "SecondaryTextBrush");
            var (State, Detail) = await GameLink.WaitForStatusAsync(dir, _context.Lifetime);
            var ok = State == ConnectionState.Disconnected;
            Show(ok ? "Disconnected" : "Error: " + Detail, ok ? "SuccessBrush" : "ErrorBrush");
        }
        catch (OperationCanceledException) when (_context.Lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Show($"Error ({ex.GetType().Name}): {ex.Message}", "ErrorBrush");
        }
        finally
        {
            _connecting = false;
            ConnectButton.IsEnabled = true;
            DisconnectButton.IsEnabled = true;
        }
    }

    private void Show(string text, string brushResource)
    {
        Status.Foreground = (IBrush)Application.Current!.FindResource(brushResource)!;
        Status.Text = text;
        _context.Log(text);
    }
}
