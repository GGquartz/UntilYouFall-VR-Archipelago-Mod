using UntilYouFall.Shared;

namespace UntilYouFall.Launcher.Core;

internal static class GameLink
{
    public static string StatusPath(string gameDir) => Path.Combine(gameDir, "UserData", LauncherProtocol.StatusFile);

    public static (ConnectionState State, string Detail)? ReadStatus(string gameDir)
    {
        try
        {
            var lines = File.ReadAllLines(StatusPath(gameDir));
            return
                lines.Length == 0
                || !Enum.TryParse<ConnectionState>(lines[0].Trim(), true, out var state)
                || !Enum.IsDefined(typeof(ConnectionState), state)
                ? null
                : (state, lines.Length > 1 ? lines[1] : "");
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static async Task<(ConnectionState State, string Detail)> WaitForStatusAsync(
        string gameDir,
        CancellationToken cancellation
    )
    {
        for (var attempt = 0; attempt < 60; attempt++)
        {
            cancellation.ThrowIfCancellationRequested();
            var status = ReadStatus(gameDir);
            if (status != null && status.Value.State != ConnectionState.Connecting)
                return status.Value;
            await Task.Delay(500, cancellation);
        }
        throw new TimeoutException(
            "Failed to get answer from the game - is the mod installed and the game finished loading?"
        );
    }

    public static void SendConnectCommand(string gameDir, string server, string slot, string password) =>
        SendCommand(gameDir, LauncherProtocol.ConnectCommand(server, slot, password));

    public static void SendDisconnectCommand(string gameDir) =>
        SendCommand(gameDir, LauncherProtocol.DisconnectCommand);

    private static void SendCommand(string gameDir, string command)
    {
        if (!ModInstaller.GameRunning())
            throw new InvalidOperationException("Until You Fall is not running");
        var request = Path.Combine(gameDir, "UserData", LauncherProtocol.CommandFile);
        if (File.Exists(request))
            throw new InvalidOperationException("A command is already pending");
        Directory.CreateDirectory(Path.GetDirectoryName(request)!);
        File.Delete(StatusPath(gameDir));
        AtomicFile.Write(request, temporary => File.WriteAllText(temporary, command), overwrite: false);
    }
}
