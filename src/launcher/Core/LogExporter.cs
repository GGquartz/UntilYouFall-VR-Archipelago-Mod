using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using UntilYouFall.Shared;

namespace UntilYouFall.Launcher.Core;

internal static class LogExporter
{
    public static string LauncherLogPath => Path.Combine(LauncherSettings.SettingsDirectory, "launcher.log");

    public static void AppendLauncherLog(string line)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LauncherLogPath)!);
            var info = new FileInfo(LauncherLogPath);
            if (info.Exists && info.Length > 2_000_000)
                File.Move(LauncherLogPath, LauncherLogPath + ".old", true);
            File.AppendAllText(LauncherLogPath, line + Environment.NewLine);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public static string RedactPreferences(string text) =>
        Regex.Replace(
            text,
            @"^([ \t]*AP(?:Password|Server|Slot)[ \t]*=)[^\r\n]*",
            "$1 \"<redacted>\"",
            RegexOptions.Multiline | RegexOptions.IgnoreCase
        );

    private static string Redact(string text)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var parent = Path.GetDirectoryName(profile);

        if (!string.IsNullOrEmpty(parent))
        {
            var anonymousProfile = Path.Combine(parent, "<UserName>");
            text = text.Replace(profile, anonymousProfile, StringComparison.OrdinalIgnoreCase);
            text = text.Replace(
                profile.Replace('\\', '/'),
                anonymousProfile.Replace('\\', '/'),
                StringComparison.OrdinalIgnoreCase
            );
        }
        text = Regex.Replace(
            text,
            @"(\b[a-z]:[\\/]+Users[\\/]+)[^\\/\r\n""<>]+",
            "$1<UserName>",
            RegexOptions.IgnoreCase
        );
        text = Regex.Replace(
            text,
            @"((?<![\w.-])|\b[a-z]:)([\\/]+home[\\/]+)[^\\/\r\n\s""<>]+",
            "$1$2<UserName>",
            RegexOptions.IgnoreCase
        );
        text = Regex.Replace(
            text,
            @"(?<prefix>\bConnect(?:ing|ed)? to[ \t]+)(?<scheme>wss?://)?[^\s;]+(?:(?<slotPrefix>; slot=| as )[^\r\n]+)?",
            match =>
                match.Groups["prefix"].Value
                + match.Groups["scheme"].Value
                + "<Server>"
                + (match.Groups["slotPrefix"].Success ? match.Groups["slotPrefix"].Value + "<SlotName>" : ""),
            RegexOptions.IgnoreCase
        );
        text = Regex.Replace(text, @"\b(wss?://)[^\s""'<>]+", "$1<Server>", RegexOptions.IgnoreCase);
        text = Regex.Replace(
            text,
            @"(?<![\w\\/.-])(?:(?:[a-z0-9-]+\.)+[a-z0-9-]+|localhost|\[[0-9a-f:]+\]):\d{1,5}\b",
            "<Server>",
            RegexOptions.IgnoreCase
        );

        return RedactPreferences(text);
    }

    public static void Export(string gameDir, string payloadDir, string zipPath, string launcherLog, string version)
    {
        AtomicFile.Write(
            zipPath,
            temporary =>
            {
                using var archive = ZipFile.Open(temporary, ZipArchiveMode.Create);
                AddText(archive, "info.txt", BuildInfoText(gameDir, payloadDir, version));
                AddFile(archive, "launcher/launcher.log", launcherLog);
                AddFile(archive, "launcher/launcher.log.old", launcherLog + ".old");

                if (GameLocator.IsGameDir(gameDir))
                {
                    AddFile(archive, "game/Latest.log", Path.Combine(gameDir, "MelonLoader", "Latest.log"));
                    AddFolder(archive, "game/Logs/", Path.Combine(gameDir, "MelonLoader", "Logs"));
                    AddFolder(archive, "game/ArchipelagoLogs/", Path.Combine(gameDir, "UserData", "ArchipelagoLogs"));
                    var unity = UnityLogFolder(gameDir);
                    AddFile(archive, "game/Unity/Player.log", Path.Combine(unity, "Player.log"));
                    AddFile(archive, "game/Unity/Player-prev.log", Path.Combine(unity, "Player-prev.log"));
                    AddFile(
                        archive,
                        "game/MelonPreferences.cfg",
                        Path.Combine(gameDir, "UserData", "MelonPreferences.cfg")
                    );
                    AddFile(archive, "game/Archipelago.status", GameLink.StatusPath(gameDir));
                }
            }
        );
    }

    private static string UnityLogFolder(string gameDir) =>
        OperatingSystem.IsLinux()
            ? LinuxSupport.ProtonUnityLogFolder(gameDir)
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low",
                "Schell Games",
                "UntilYouFall"
            );

    private static string BuildInfoText(string gameDir, string payloadDir, string version)
    {
        var text = new StringBuilder();
        text.AppendLine($"Launcher: {version}");
        text.AppendLine($"Time (UTC): {DateTime.UtcNow:u}");
        text.AppendLine($"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        text.AppendLine($"Game folder: {gameDir}");
        if (OperatingSystem.IsLinux())
            text.AppendLine($"Proton prefix found: {Directory.Exists(LinuxSupport.ProtonUnityLogFolder(gameDir))}");
        text.AppendLine("Component versions (package / installed):");
        foreach (var entry in VersionInfo.Read(gameDir, payloadDir))
            text.AppendLine($"  {entry.Component}: {entry.Package} / {entry.Installed}");

        if (GameLocator.IsGameDir(gameDir))
        {
            text.AppendLine(
                $"MelonLoader installed: {MelonLoaderInstaller.IsInstalled(gameDir)} (assemblies generated: {MelonLoaderInstaller.AssembliesGenerated(gameDir)})"
            );
            text.AppendLine($"Game running: {ModInstaller.GameRunning()}");
            text.AppendLine("Mod files:");
            foreach (var entry in ModInstaller.Status(gameDir, payloadDir))
                text.AppendLine($"  {entry.Relative}: {entry.State}");
        }
        else
            text.AppendLine("Game folder is not valid");
        return text.ToString();
    }

    private static void AddFolder(ZipArchive archive, string prefix, string folder)
    {
        if (!Directory.Exists(folder))
            return;
        foreach (var file in Directory.GetFiles(folder, "*.log"))
            AddFile(archive, prefix + Path.GetFileName(file), file);
    }

    private static void AddText(ZipArchive archive, string name, string text)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(Redact(text));
    }

    private static void AddFile(ZipArchive archive, string name, string path)
    {
        if (!File.Exists(path))
            return;
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete
            );
            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            AddText(archive, name, content);
        }
        catch (IOException ex)
        {
            AddText(archive, name + ".error.txt", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            AddText(archive, name + ".error.txt", ex.Message);
        }
    }
}
