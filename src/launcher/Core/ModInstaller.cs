using System.Diagnostics;
using System.Security.Cryptography;

namespace UntilYouFall.Launcher.Core;

internal enum FileState
{
    Missing,
    Different,
    Current,
}

internal sealed record InstallEntry(string Relative, FileState State);

internal static class ModInstaller
{
    public static bool GameRunning() =>
        OperatingSystem.IsLinux()
            ? LinuxSupport.GameProcessRunning()
            : Process.GetProcessesByName("UntilYouFall").Length > 0;

    public static IReadOnlyList<InstallEntry> Status(string gameDir, string payloadDir) =>
        PayloadFiles(payloadDir)
            .Select(relative =>
            {
                var target = Path.Combine(gameDir, relative);
                var state =
                    !File.Exists(target) ? FileState.Missing
                    : Hash(target) == Hash(Path.Combine(payloadDir, relative)) ? FileState.Current
                    : FileState.Different;

                return new InstallEntry(relative, state);
            })
            .ToList();

    public static IEnumerable<string> PayloadFiles(string payloadDir) =>
        Directory.Exists(payloadDir)
            ? Directory
                .EnumerateFiles(payloadDir, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(payloadDir, path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            : Enumerable.Empty<string>();

    public static int Install(string gameDir, string payloadDir)
    {
        if (PayloadFiles(payloadDir).Any() == false)
            throw new InvalidOperationException("The launcher is missing the payload folder");
        ThrowIfRunning();
        var copied = 0;
        foreach (var entry in Status(gameDir, payloadDir).Where(entry => entry.State != FileState.Current))
        {
            var source = Path.Combine(payloadDir, entry.Relative);
            var target = Path.Combine(gameDir, entry.Relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
            if (Hash(source) != Hash(target))
                throw new IOException($"The copied file does not match the package: {entry.Relative}");
            copied++;
        }
        return copied;
    }

    public static int Uninstall(string gameDir, string payloadDir)
    {
        ThrowIfRunning();
        var removed = 0;
        foreach (var relative in PayloadFiles(payloadDir))
        {
            var target = Path.Combine(gameDir, relative);
            if (!File.Exists(target))
                continue;
            File.Delete(target);
            removed++;
        }
        return removed;
    }

    private static void ThrowIfRunning()
    {
        if (GameRunning())
            throw new InvalidOperationException("Close the game first");
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
