using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace UntilYouFall.Launcher.Core;

internal static class GameLocator
{
    public const string Executable = "UntilYouFall.exe";

    public const string SteamAppId = "858260";
    private const string DefaultInstallDir = "UntilYouFall";
    private const string DataDirectorySuffix = "_Data";

    public static bool IsGameDir(string? dir) =>
        !string.IsNullOrWhiteSpace(dir) && File.Exists(Path.Combine(dir, Executable));

    public static string? Find() => Candidates(SteamRoots()).FirstOrDefault(IsGameDir);

    public static string Normalize(string? input)
    {
        var path = (input ?? "").Trim(' ', '\t', '"').TrimEnd('\\', '/');
        var name = Path.GetFileName(path);
        var isGameFile = name.Equals(Executable, StringComparison.OrdinalIgnoreCase);
        var isDataFolder = name.EndsWith(DataDirectorySuffix, StringComparison.OrdinalIgnoreCase);
        return isGameFile || isDataFolder ? Path.GetDirectoryName(path) ?? path : path;
    }

    public static IEnumerable<string> Candidates(IEnumerable<string> steamRoots)
    {
        var candidates = SteamLibraries(steamRoots)
            .SelectMany(library =>
            {
                var steamApps = Path.Combine(library, "steamapps");
                var installDir = InstallDirectory(
                    ReadText(Path.Combine(steamApps, $"appmanifest_{SteamAppId}.acf")) ?? ""
                );
                return new[] { installDir, DefaultInstallDir }
                    .Where(name => name != null)
                    .Select(name => Path.Combine(steamApps, "common", name!));
            });
        return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    public static IEnumerable<string> LibraryPaths(string vdf) =>
        Regex.Matches(vdf, "\"path\"\\s+\"([^\"]+)\"").Select(match => match.Groups[1].Value.Replace(@"\\", @"\"));

    public static string? InstallDirectory(string manifest)
    {
        var match = Regex.Match(manifest, "\"installdir\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static IEnumerable<string> SteamLibraries(IEnumerable<string> steamRoots)
    {
        foreach (var steam in steamRoots)
        {
            yield return steam;
            var vdf =
                ReadText(Path.Combine(steam, "config", "libraryfolders.vdf"))
                ?? ReadText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"));
            if (vdf == null)
                continue;
            foreach (var library in LibraryPaths(vdf))
                yield return library;
        }
    }

    private static IEnumerable<string> SteamRoots()
    {
        var roots = new List<string>();
        if (OperatingSystem.IsLinux())
            roots.AddRange(LinuxSupport.SteamRoots(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
        else if (OperatingSystem.IsWindows())
            roots.AddRange(WindowsSteamRoots());

        return roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> WindowsSteamRoots()
    {
        var roots = new List<string>();
        foreach (
            var (hive, key, name) in new[]
            {
                (Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
                (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
                (Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath"),
            }
        )
        {
            using var steam = hive.OpenSubKey(key);
            if (steam?.GetValue(name) is string path && path.Length > 0)
                roots.Add(path.Replace('/', '\\'));
        }

        return roots;
    }

    private static string? ReadText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
