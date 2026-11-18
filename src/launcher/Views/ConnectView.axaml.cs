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

    private bool _dirty;
    private bool _loading;

    public ConnectView()
    {
        InitializeComponent();
        foreach (var box in new[] { Server, Slot, Password })
            box.TextChanged += (_, _) => _dirty |= !_loading && box.IsFocused;
    }

    internal void Initialize(LauncherContext context) => _context = context;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_dirty)
            return;

        var path = ConnectionPath();
        if (path == null || ConnectionFile.TryRead(path) is not { } details)
            return;

        _loading = true;
        Server.Text = details.Server;
        Slot.Text = details.Slot;
        Password.Text = details.Password;
        _loading = false;
    }

    private string? ConnectionPath()
    {
        var dir = _context.GameDirectoryText();
        return GameLocator.IsGameDir(dir) ? GameLink.ConnectionPath(dir) : null;
    }

    public void SaveConnection()
    {
        if (!_dirty || ConnectionPath() is not { } path)
            return;

        try
        {
            ConnectionFile.Write(path, new ConnectionDetails(Server.Text ?? "", Slot.Text ?? "", Password.Text ?? ""));
            _dirty = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _context.Log("Failed to save the connection file: " + ex.Message);
        }
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
            var dir = _context.GetGameDirectory();
            var server = Server.Text?.Trim() ?? "";
            var slot = Slot.Text?.Trim() ?? "";
            var password = Password.Text ?? "";
            LauncherProtocol.ConnectCommand(server, slot, password);
            _dirty = true;
            SaveConnection();
            if (ModInstaller.GameRunning() && GameLink.ReadStatus(dir)?.State == ConnectionState.Connected)
            {
                Show("Already connected", "SecondaryTextBrush");
            }
            else if (ModInstaller.GameRunning())
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
