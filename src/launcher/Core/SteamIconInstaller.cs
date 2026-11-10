using System.Security.Cryptography;
using UntilYouFall.Shared;

namespace UntilYouFall.Launcher.Core;

// Download steam achievement icons for the quest board during install to avoid redistributing assets in our repo
internal static class SteamIconInstaller
{
    private static readonly string[] Hosts =
    {
        "cdn.akamai.steamstatic.com",
        "cdn.cloudflare.steamstatic.com",
        "cdn.fastly.steamstatic.com",
        "steamcdn-a.akamaihd.net",
    };

    public static string IconFolder(string gameDir) =>
        Path.Combine(gameDir, "UserData", SteamQuestIcons.FolderRelativeToUserData);

    public static int MissingCount(string gameDir, IReadOnlyDictionary<string, string>? icons = null) =>
        (icons ?? SteamQuestIcons.Sha1ByIconId).Keys.Count(id =>
            !File.Exists(Path.Combine(IconFolder(gameDir), SteamQuestIcons.FileName(id)))
        );

    public static string Url(string host, string sha1) =>
        $"https://{host}/steamcommunity/public/images/apps/{GameLocator.SteamAppId}/{sha1}.jpg";

    public static async Task<(int Downloaded, int Failed)> InstallAsync(
        string gameDir,
        Func<string, CancellationToken, Task<byte[]>>? fetch = null,
        CancellationToken cancel = default,
        IReadOnlyDictionary<string, string>? icons = null
    )
    {
        var folder = IconFolder(gameDir);
        Directory.CreateDirectory(folder);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        fetch ??= (url, token) => http.GetByteArrayAsync(url, token);
        var pending = new List<(string Target, string Sha1)>();
        foreach (var (iconId, sha1) in icons ?? SteamQuestIcons.Sha1ByIconId)
        {
            var target = Path.Combine(folder, SteamQuestIcons.FileName(iconId));
            if (File.Exists(target) && Sha1Of(await File.ReadAllBytesAsync(target, cancel)) == sha1)
                continue;
            pending.Add((target, sha1));
        }

        var downloaded = 0;
        var failed = 0;
        foreach (var (target, sha1) in pending)
        {
            var result = await TryDownloadAsync(fetch, sha1, target, cancel);
            if (result == DownloadResult.Saved)
            {
                downloaded++;
                continue;
            }
            failed++;
            if (result == DownloadResult.Unreachable)
                break;
        }

        return (downloaded, failed + (pending.Count - downloaded - failed));
    }

    public static void Uninstall(string gameDir)
    {
        var folder = IconFolder(gameDir);
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
    }

    private enum DownloadResult
    {
        Saved,
        Rejected,
        Unreachable,
    }

    private static async Task<DownloadResult> TryDownloadAsync(
        Func<string, CancellationToken, Task<byte[]>> fetch,
        string sha1,
        string target,
        CancellationToken cancel
    )
    {
        var reachable = false;
        foreach (var host in Hosts)
        {
            try
            {
                var bytes = await fetch(Url(host, sha1), cancel);
                reachable = true;
                if (Sha1Of(bytes) != sha1)
                    continue;
                await File.WriteAllBytesAsync(target, bytes, cancel);

                return DownloadResult.Saved;
            }
            catch (Exception ex)
                when (ex is HttpRequestException or TaskCanceledException && !cancel.IsCancellationRequested)
            {
                reachable |= ex is HttpRequestException { StatusCode: not null };
            }
        }

        return reachable ? DownloadResult.Rejected : DownloadResult.Unreachable;
    }

    private static string Sha1Of(byte[] bytes) => Convert.ToHexString(SHA1.HashData(bytes)).ToLowerInvariant();
}
