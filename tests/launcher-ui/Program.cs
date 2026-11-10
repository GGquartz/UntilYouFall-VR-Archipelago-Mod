using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using UntilYouFall.Launcher;
using UntilYouFall.Launcher.Core;
using UntilYouFall.Launcher.Views;

// Walks through the whole player flow in a headless window: find the game, install, copy the launch option, export logs.
// On Linux it runs against a fake Steam library under a fake HOME; on Windows the game folder is typed in instead.
// Screenshots are written only when a folder is given as the first argument.

var screenshotFolder = args.Length > 0 ? Path.GetFullPath(args[0]) : null;
if (screenshotFolder != null)
{
    Directory.CreateDirectory(screenshotFolder);
}
var root = Path.Combine(Path.GetTempPath(), "uyf-ui-" + Guid.NewGuid().ToString("N"));
var home = Path.Combine(root, "home");
var library = Path.Combine(home, "SteamLibrary");
var gameDir = Path.Combine(library, "steamapps", "common", "UntilYouFall");
var failed = 0;

void Check(string name, bool condition)
{
    Console.WriteLine((condition ? "PASS " : "FAIL ") + name);
    if (!condition)
        failed++;
}

Environment.SetEnvironmentVariable("UYF_LAUNCHER_DATA", Path.Combine(root, "launcher-data"));
if (OperatingSystem.IsLinux())
    Environment.SetEnvironmentVariable("HOME", home);

BuildFakeSteamLibrary();
BuildFakePayload();

using var session = HeadlessUnitTestSession.StartNew(typeof(UiTestApp));
try
{
    await session.Dispatch(
        async () =>
        {
            await RunPlayerFlow();
            return 0;
        },
        CancellationToken.None
    );
}
catch (Exception exception)
{
    Console.WriteLine("FAIL unexpected exception: " + exception);
    failed++;
}

Console.WriteLine(failed == 0 ? "All launcher UI tests passed." : $"{failed} launcher UI checks failed.");
return failed == 0 ? 0 : 1;

async Task RunPlayerFlow()
{
    var window = new MainWindow();
    window.Show();
    await Settle();

    if (OperatingSystem.IsLinux())
    {
        Check("linux guide is visible on Linux", Find<Border>(window, "LinuxGuide").IsVisible);
        Click(window, "Detect");
        await WaitFor(
            () => Find<TextBox>(window, "GameDir").Text == gameDir,
            "Detect finds the game in a second Steam library"
        );
    }
    else
    {
        Find<Border>(window, "LinuxGuide").IsVisible = true;
        Find<TextBox>(window, "GameDir").Text = gameDir;
        Click(window, "Refresh");
    }
    await WaitFor(() => Find<TextBlock>(window, "MelonStatus").Text == "MelonLoader missing", "status before install");

    Click(window, "Install / Repair");
    await WaitFor(
        () => !Find<InstallView>(window, "InstallView").IsInstalling && Find<TextBlock>(window, "Ready").Text != "",
        "install finishes",
        TimeSpan.FromMinutes(3)
    );
    Check("MelonLoader installed", MelonLoaderInstaller.IsInstalled(gameDir));
    Check("mod copied", File.Exists(Path.Combine(gameDir, "Mods", "UntilYouFall.Archipelago.dll")));
    Check("libraries copied", File.Exists(Path.Combine(gameDir, "UserLibs", "Newtonsoft.Json.dll")));
    Check("game is not reported as running", !ModInstaller.GameRunning());
    var ready = Find<TextBlock>(window, "Ready").Text ?? "";
    Check(
        "ready message points to the launch option on Linux",
        !OperatingSystem.IsLinux() || ready.Contains("launch option", StringComparison.OrdinalIgnoreCase)
    );

    Check("launch option is shown", Find<TextBox>(window, "LaunchOption").Text == LinuxSupport.LaunchOption);
    Click(window, "Copy");
    var clipboard = TopLevel.GetTopLevel(window)?.Clipboard;
    Check("window has a clipboard", clipboard != null);
    if (clipboard != null)
    {
        await WaitFor(
            () => Find<TextBox>(window, "Log").Text?.Contains("Launch option copied") == true,
            "copy confirmation in the log"
        );
        Check("launch option is on the clipboard", await clipboard.GetTextAsync() == LinuxSupport.LaunchOption);
    }
    await Screenshot(window, "install-launch-option.png");

    if (OperatingSystem.IsLinux())
        CheckLogExport();
    window.Close();
}

void CheckLogExport()
{
    var zip = Path.Combine(root, "logs.zip");
    LogExporter.Export(
        gameDir,
        Path.Combine(AppContext.BaseDirectory, "payload"),
        zip,
        LogExporter.LauncherLogPath,
        "test"
    );
    using var archive = System.IO.Compression.ZipFile.OpenRead(zip);
    var player = archive.GetEntry("game/Unity/Player.log");
    Check("export finds Player.log in the Proton prefix", player != null);
    if (player == null)
        return;
    using var reader = new StreamReader(player.Open());
    var text = reader.ReadToEnd();
    Check("export masks the Linux user name", !text.Contains("privateplayer") && text.Contains("/home/<UserName>/"));
    using var info = new StreamReader(archive.GetEntry("info.txt")!.Open());
    Check("export info reports the Proton prefix", info.ReadToEnd().Contains("Proton prefix found: True"));
}

void BuildFakeSteamLibrary()
{
    Directory.CreateDirectory(gameDir);
    File.WriteAllText(Path.Combine(gameDir, GameLocator.Executable), "");
    var steamApps = Path.Combine(library, "steamapps");
    File.WriteAllText(
        Path.Combine(steamApps, "appmanifest_858260.acf"),
        "\"AppState\"\n{\n\t\"appid\"\t\t\"858260\"\n\t\"installdir\"\t\t\"UntilYouFall\"\n}"
    );
    var unityLogs = LinuxSupport.ProtonUnityLogFolder(gameDir);
    Directory.CreateDirectory(unityLogs);
    File.WriteAllText(
        Path.Combine(unityLogs, "Player.log"),
        "Loaded /home/privateplayer/.local/share/Steam/steamapps/common/UntilYouFall/UntilYouFall.exe\n"
    );
    if (!OperatingSystem.IsLinux())
        return;
    var steamRoot = Path.Combine(home, ".local", "share", "Steam");
    Directory.CreateDirectory(Path.Combine(steamRoot, "config"));
    File.WriteAllText(
        Path.Combine(steamRoot, "config", "libraryfolders.vdf"),
        "\"libraryfolders\"\n{\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"" + library + "\"\n\t}\n}"
    );
}

void BuildFakePayload()
{
    var payload = Path.Combine(AppContext.BaseDirectory, "payload");
    if (Directory.Exists(payload))
        Directory.Delete(payload, true);
    foreach (
        var relative in new[]
        {
            "Mods/UntilYouFall.Archipelago.dll",
            "UserLibs/Archipelago.MultiClient.Net.dll",
            "UserLibs/Newtonsoft.Json.dll",
        }
    )
    {
        var path = Path.Combine(payload, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "placeholder " + relative);
    }
}

T Find<T>(Window window, string name)
    where T : Control => window.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

void Click(Window window, string caption) =>
    window
        .GetVisualDescendants()
        .OfType<Button>()
        .First(button => button.Content as string == caption)
        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

async Task Settle()
{
    for (var i = 0; i < 5; i++)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        await Task.Delay(50);
    }
}

async Task WaitFor(Func<bool> condition, string name, TimeSpan? timeout = null)
{
    var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
    while (!condition() && DateTime.UtcNow < deadline)
    {
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(100);
    }
    Check("waited for: " + name, condition());
}

async Task Screenshot(Window window, string fileName)
{
    if (screenshotFolder == null)
    {
        return;
    }

    await Settle();
    var frame = window.CaptureRenderedFrame();
    Check("screenshot rendered: " + fileName, frame != null);
    frame?.Save(Path.Combine(screenshotFolder, fileName));
}

public sealed class UiTestApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder
            .Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
