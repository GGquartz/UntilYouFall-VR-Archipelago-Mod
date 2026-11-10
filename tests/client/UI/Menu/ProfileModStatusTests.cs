using System.Reflection;

namespace UntilYouFallArchipelago;

internal static class ProfileModStatusTests
{
    public static void Run()
    {
        var formatter = typeof(ModMetadata).GetMethod(
            "ConnectionStatusText",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
        );
        if (formatter == null)
            throw new Exception("profile status formatter exists");
        Assert(
            (string?)formatter.Invoke(null, [false])
                == $"Archipelago Mod Loaded - v{ModMetadata.Version} - <color=#FF5A5A>Disconnected</color>",
            "profile status identifies a disconnected client"
        );
        Assert(
            (string?)formatter.Invoke(null, [true])
                == $"Archipelago Mod Loaded - v{ModMetadata.Version} - <color=#62E86F>Connected</color>",
            "profile status identifies a connected client"
        );
        Console.WriteLine("PASS: profile menu status shows mod version and live connection state");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
