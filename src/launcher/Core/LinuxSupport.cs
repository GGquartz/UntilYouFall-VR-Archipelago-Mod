namespace UntilYouFall.Launcher.Core;

internal static class LinuxSupport
{
    public const string LaunchOption = "WINEDLLOVERRIDES=\"version=n,b\" %command%";

    private const string UnityLogFolder = "drive_c/users/steamuser/AppData/LocalLow/Schell Games/UntilYouFall";

    public static IEnumerable<string> SteamRoots(string home) =>
        new[]
        {
            ".steam/steam",
            ".local/share/Steam",
            ".var/app/com.valvesoftware.Steam/.local/share/Steam",
            "snap/steam/common/.local/share/Steam",
        }.Select(relative => Path.Combine(home, relative));

    public static string ProtonUnityLogFolder(string gameDir)
    {
        var steamApps = Path.GetDirectoryName(Path.GetDirectoryName(gameDir.TrimEnd('/', '\\')));
        return Path.Combine(steamApps ?? "", "compatdata", GameLocator.SteamAppId, "pfx", UnityLogFolder);
    }

    public static bool IsGameCommandLine(string commandLine) =>
        commandLine.Contains(GameLocator.Executable, StringComparison.OrdinalIgnoreCase);

    public static bool GameProcessRunning()
    {
        foreach (var process in Directory.EnumerateDirectories("/proc"))
        {
            try
            {
                if (IsGameCommandLine(File.ReadAllText(Path.Combine(process, "cmdline"))))
                    return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Process ended
            }
        }

        return false;
    }
}
