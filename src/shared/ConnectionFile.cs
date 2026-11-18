namespace UntilYouFall.Shared;

internal readonly record struct ConnectionDetails(string Server, string Slot, string Password);

internal static class ConnectionFile
{
    public const string FileName = "Archipelago.connection.txt";
    private const string DefaultServer = "archipelago.gg:12345";

    public static string Format(ConnectionDetails details) =>
        string.Join(
            "\n",
            "Server=" + (string.IsNullOrWhiteSpace(details.Server) ? DefaultServer : details.Server),
            "Slot=" + details.Slot,
            "Password=" + details.Password,
            ""
        );

    public static void Write(string path, ConnectionDetails details)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        AtomicFile.Write(path, temporary => File.WriteAllText(temporary, Format(details)));
    }

    public static ConnectionDetails? TryRead(string path)
    {
        try
        {
            return File.Exists(path) ? ReadFields(File.ReadAllText(path)) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    public static ConnectionDetails Parse(string text)
    {
        var details = ReadFields(text);
        LauncherProtocol.ConnectCommand(details.Server, details.Slot, details.Password);
        return details;
    }

    private static ConnectionDetails ReadFields(string text)
    {
        string server = "",
            slot = "",
            password = "";
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var separator = line.IndexOf('=');
            if (separator < 0)
                throw new ArgumentException("Connection line format error");

            var value = line[(separator + 1)..].Trim();
            switch (line[..separator].Trim().ToLowerInvariant())
            {
                case "server":
                    server = value;
                    break;
                case "slot":
                    slot = value;
                    break;
                case "password":
                    password = value;
                    break;
                default:
                    throw new ArgumentException("Unknown connection file setting: " + line[..separator].Trim());
            }
        }

        return new ConnectionDetails(server, slot, password);
    }
}
