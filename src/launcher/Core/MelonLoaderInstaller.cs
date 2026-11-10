using System.IO.Compression;
using System.Security.Cryptography;

namespace UntilYouFall.Launcher.Core;

internal static class MelonLoaderInstaller
{
    public const string Version = "0.7.3";
    public const string Url = "https://github.com/LavaGang/MelonLoader/releases/download/v0.7.3/MelonLoader.x64.zip";
    public const string Sha256 = "5B2B2F3D1CD42B59EC886C5BDC2663EDAE87A0097A4F4A8F58C0965A99DDA416";

    public static bool IsInstalled(string gameDir) =>
        File.Exists(Path.Combine(gameDir, "version.dll"))
        && File.Exists(Path.Combine(gameDir, "MelonLoader", "net6", "MelonLoader.dll"));

    public static bool AssembliesGenerated(string gameDir)
    {
        var folder = Path.Combine(gameDir, "MelonLoader", "Il2CppAssemblies");
        return Directory.Exists(folder) && Directory.EnumerateFiles(folder, "*.dll").Any();
    }

    public static async Task InstallAsync(string gameDir, CancellationToken cancel = default)
    {
        if (ModInstaller.GameRunning())
            throw new InvalidOperationException("Close the game first");

        var zip = Path.Combine(Path.GetTempPath(), "MelonLoader." + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("UntilYouFallLauncher/" + Version);
                await using var download = await http.GetStreamAsync(Url, timeout.Token);
                await using var file = File.Create(zip);
                await download.CopyToAsync(file, timeout.Token);
            }
            await Task.Run(() => Extract(zip, gameDir), cancel);
        }
        catch (OperationCanceledException ex) when (!cancel.IsCancellationRequested)
        {
            throw new TimeoutException("MelonLoader download timed out, try later or check logs", ex);
        }
        finally
        {
            if (File.Exists(zip))
                File.Delete(zip);
        }
    }

    public static void Extract(string zipPath, string gameDir)
    {
        using (var stream = File.OpenRead(zipPath))
            if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("MelonLoader does not match the expected hash, try again or check logs");
        ZipFile.ExtractToDirectory(zipPath, gameDir, overwriteFiles: true);
    }
}
