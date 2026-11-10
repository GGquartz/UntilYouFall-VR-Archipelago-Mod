using System.Runtime.InteropServices;
using Avalonia;

namespace UntilYouFall.Launcher;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (MissingLinuxLibrary() is { } missing)
        {
            Console.Error.WriteLine(
                $"Missing system library {missing} - Install it (Debian/Ubuntu: sudo apt install libice6 libsm6; Fedora: sudo dnf install libICE libSM) and start the launcher again"
            );
            return 1;
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();

    private static string? MissingLinuxLibrary()
    {
        if (!OperatingSystem.IsLinux())
        {
            return null;
        }

        foreach (var library in new[] { "libICE.so.6", "libSM.so.6" })
        {
            if (!NativeLibrary.TryLoad(library, out var handle))
            {
                return library;
            }

            NativeLibrary.Free(handle);
        }

        return null;
    }
}
