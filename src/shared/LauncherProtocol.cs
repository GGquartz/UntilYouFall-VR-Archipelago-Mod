namespace UntilYouFall.Shared;

internal enum ConnectionState
{
    Connecting,
    Connected,
    Error,
    Disconnected,
}

internal static class LauncherProtocol
{
    public const string PreferencesSection = "UntilYouFallArchipelago";
    public const string DisconnectCommand = "Disconnect";
    public const string CommandFile = "Archipelago.command.request";
    public const string StatusFile = "Archipelago.status";

    public static string ConnectCommand(string server, string slot, string password)
    {
        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(slot))
            throw new ArgumentException("Enter a server and a slot name");
        if (new[] { server, slot, password }.Any(value => value.Contains('\n') || value.Contains('\r')))
            throw new ArgumentException("Connection fields cannot contain newlines");

        ServerEndpoint(server);
        return string.Join("\n", "Connect", server, slot, password);
    }

    public static string ServerEndpoint(string server)
    {
        var uri = new Uri(server.Contains("://") ? server : "ws://" + server);
        if (uri.Scheme != "ws" && uri.Scheme != "wss")
            throw new ArgumentException("AP server address must use ws:// or wss://");
        return uri.AbsoluteUri.TrimEnd('/');
    }
}
