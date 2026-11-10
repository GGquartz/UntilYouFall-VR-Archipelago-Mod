using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

namespace UntilYouFall.Launcher.Core;

internal sealed record ComponentVersion(string Component, string Package, string Installed);

internal static class VersionInfo
{
    public static string LauncherVersion => typeof(VersionInfo).Assembly.GetName().Version!.ToString(3);

    public static IReadOnlyList<ComponentVersion> Read(string gameDir, string payloadDir)
    {
        using var stream = typeof(VersionInfo).Assembly.GetManifestResourceStream("archipelago.json")!;
        using var manifest = JsonDocument.Parse(stream);
        var versions = new List<ComponentVersion>
        {
            new("Launcher", LauncherVersion, "-"),
            new("APWorld", manifest.RootElement.GetProperty("world_version").GetString()!, "-"),
            new("Archipelago (minimum)", manifest.RootElement.GetProperty("minimum_ap_version").GetString()!, "-"),
        };
        var hasGame = GameLocator.IsGameDir(gameDir);
        foreach (
            var (component, relative, package) in new[]
            {
                ("Game mod", Path.Combine("Mods", "UntilYouFall.Archipelago.dll"), (string?)null),
                ("MelonLoader", Path.Combine("MelonLoader", "net6", "MelonLoader.dll"), MelonLoaderInstaller.Version),
                (
                    "Archipelago.MultiClient.Net",
                    Path.Combine("UserLibs", "Archipelago.MultiClient.Net.dll"),
                    (string?)null
                ),
                ("Newtonsoft.Json", Path.Combine("UserLibs", "Newtonsoft.Json.dll"), (string?)null),
            }
        )
        {
            versions.Add(
                new(
                    component,
                    package ?? ReadFile(Path.Combine(payloadDir, relative), "Not included"),
                    hasGame ? ReadFile(Path.Combine(gameDir, relative), "Not installed") : "Game folder not selected"
                )
            );
        }

        return versions;
    }

    private static string ReadFile(string path, string missing)
    {
        if (!File.Exists(path))
            return missing;
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            var version = string.IsNullOrWhiteSpace(info.ProductVersion) ? info.FileVersion : info.ProductVersion;

            return string.IsNullOrWhiteSpace(version) ? "Unknown" : version.Split('+')[0];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception)
        {
            return "Unknown";
        }
    }
}
