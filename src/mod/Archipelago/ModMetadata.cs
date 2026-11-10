namespace UntilYouFallArchipelago;

internal static class ModMetadata
{
    public const string Version = "0.1.0";
    public const string StatusText = "Archipelago Mod Loaded - v" + Version;

    public static string ConnectionStatusText(bool connected) =>
        $"{StatusText} - "
        + (
            connected
                ? ApColors.Rich(ApColors.Connected, "Connected")
                : ApColors.Rich(ApColors.Disconnected, "Disconnected")
        );
}
