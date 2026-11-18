using UntilYouFall.Shared;

namespace UntilYouFall.Launcher.Core;

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
