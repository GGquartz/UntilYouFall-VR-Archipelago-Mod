namespace UntilYouFallArchipelago;

internal static partial class ApColors
{
    // AP
    public const string Progression = "AF99EF";
    public const string Useful = "6D8BE8";
    public const string Salmon = "FA8072";
    public const string Filler = "00EEEE";
    public const string Player = "FAFAD2";
    public const string Magenta = "EE00EE";
    public const string Green = "00FF7F";
    public const string Blue = "6495ED";
    public const string Red = "EE0000";

    // Mod
    public const string Location = "C4CEDD";
    public const string Gold = "F2B82E";
    public const string ModAccent = "A6E6FF";
    public const string Connected = "62E86F";
    public const string Disconnected = "FF5A5A";

    public static string Rich(string hex, string text) => $"<color=#{hex}>{text}</color>";
}
