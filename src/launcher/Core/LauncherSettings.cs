using System.Text.Json;
using UntilYouFall.Shared;

namespace UntilYouFall.Launcher.Core;

internal sealed record ConnectionSettings(string Server, string Slot);

internal static class LauncherSettings
{
    public static string SettingsDirectory =>
        Environment.GetEnvironmentVariable("UYF_LAUNCHER_DATA") is { Length: > 0 } overridePath
            ? overridePath
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "UntilYouFallLauncher"
            );

    private static string FilePath => Path.Combine(SettingsDirectory, "game-dir.txt");

    public static ConnectionSettings? LoadConnection(string? path = null)
    {
        path ??= Path.Combine(SettingsDirectory, "connection.json");
        try
        {
            if (!File.Exists(path))
                return null;
            var settings = JsonSerializer.Deserialize<ConnectionSettings>(File.ReadAllText(path));
            return settings?.Server != null && settings.Slot != null ? settings : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool SaveConnection(ConnectionSettings settings, string? path = null)
    {
        path ??= Path.Combine(SettingsDirectory, "connection.json");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AtomicFile.Write(path, temporary => File.WriteAllText(temporary, JsonSerializer.Serialize(settings)));
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static string? LoadGameDir()
    {
        try
        {
            return File.Exists(FilePath) ? File.ReadAllText(FilePath).Trim() : null;
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

    public static bool SaveGameDir(string dir)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            AtomicFile.Write(FilePath, temporary => File.WriteAllText(temporary, dir));
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
