using UntilYouFall.Launcher.Core;

var directory = Path.Combine(Path.GetTempPath(), "uyf-launcher-settings-" + Guid.NewGuid());
var path = Path.Combine(directory, "settings", "connection.json");
var checks = 0;
void Check(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
    checks++;
}

try
{
    Check(LauncherSettings.LoadConnection(path) == null, "missing settings preserve UI defaults");
    var first = new ConnectionSettings("archipelago.gg:58880", "Player č. 1");
    Check(LauncherSettings.SaveConnection(first, path), "first save creates the settings directory");
    Check(LauncherSettings.LoadConnection(path) == first, "server, port and slot survive reopening");
    Check(!File.ReadAllText(path).Contains("password", StringComparison.OrdinalIgnoreCase), "password is never stored");
    var next = new ConnectionSettings("wss://example.test:1234", "Another player");
    Check(LauncherSettings.SaveConnection(next, path), "last connection overwrites the previous one");
    Check(LauncherSettings.LoadConnection(path) == next, "the most recent connection is restored");
    Check(
        !Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.tmp").Any(),
        "atomic saves leave no temporary files"
    );
    foreach (
        var invalid in new[]
        {
            "{",
            "null",
            "{}",
            "[]",
            "{\"Server\":null,\"Slot\":\"P\"}",
            "{\"Server\":\"h:1\",\"Slot\":12}",
        }
    )
    {
        File.WriteAllText(path, invalid);
        Check(LauncherSettings.LoadConnection(path) == null, "damaged settings preserve UI defaults");
        Check(File.ReadAllText(path) == invalid, "loading never rewrites a damaged settings file");
    }
    Check(
        LauncherSettings.LoadConnection(Path.GetDirectoryName(path)!) == null,
        "an unreadable settings path does not prevent startup"
    );
    var blocked = Path.Combine(directory, "blocked");
    File.WriteAllText(blocked, "keep");
    Check(
        !LauncherSettings.SaveConnection(first, Path.Combine(blocked, "connection.json")),
        "a save failure is reported instead of thrown"
    );
    Check(File.ReadAllText(blocked) == "keep", "a failed save cannot destroy another file");
    Console.WriteLine($"PASS: {checks} launcher connection settings checks");
}
finally
{
    if (Directory.Exists(directory))
        Directory.Delete(directory, true);
}
