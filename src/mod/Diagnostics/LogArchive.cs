using MelonLoader.Utils;

namespace UntilYouFallArchipelago;

internal static class LogArchive
{
    public static void Run(Action<string> log)
    {
        try
        {
            var target = Path.Combine(MelonEnvironment.UserDataDirectory, "ArchipelagoLogs");
            Directory.CreateDirectory(target);
            var logs = Path.Combine(MelonEnvironment.MelonLoaderDirectory, "Logs");
            if (Directory.Exists(logs))
                foreach (var file in Directory.GetFiles(logs, "*.log"))
                    CopyIfMissing(file, Path.Combine(target, Path.GetFileName(file)));
            var unity = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low",
                "Schell Games",
                "UntilYouFall",
                "Player-prev.log"
            );
            if (File.Exists(unity))
                CopyIfMissing(
                    unity,
                    Path.Combine(target, "Unity_" + File.GetLastWriteTime(unity).ToString("yy-MM-dd_H-m-s") + ".log")
                );
        }
        catch (Exception ex)
        {
            log.Write("AP", "Log archive fail: " + ex.Message);
        }
    }

    private static void CopyIfMissing(string source, string destination)
    {
        if (!File.Exists(destination))
            File.Copy(source, destination);
    }
}
