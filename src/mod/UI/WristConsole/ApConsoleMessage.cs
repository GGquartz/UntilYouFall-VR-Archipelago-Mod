using System.Text;
using Archipelago.MultiClient.Net.Colors;
using Archipelago.MultiClient.Net.MessageLog.Messages;

namespace UntilYouFallArchipelago;

internal sealed record ApConsoleMessage(string PlainText, string RichText)
{
    public static ApConsoleMessage FromLog(LogMessage message)
    {
        var rich = new StringBuilder();
        var remaining = 160;
        foreach (var part in message.Parts)
        {
            var text = part.Text.Replace("\r", " ").Replace("\n", " ");
            var displayed = text[..Math.Min(text.Length, remaining)];
            var color = part.Color;
            var hex = part.PaletteColor switch
            {
                PaletteColor.Plum => ApColors.Progression,
                PaletteColor.SlateBlue => ApColors.Useful,
                PaletteColor.Salmon => ApColors.Salmon,
                PaletteColor.Cyan => ApColors.Filler,
                PaletteColor.Yellow => ApColors.Player,
                PaletteColor.Magenta => ApColors.Magenta,
                PaletteColor.Green => ApColors.Green,
                PaletteColor.Blue => ApColors.Blue,
                PaletteColor.Red => ApColors.Red,
                _ => $"{color.R:X2}{color.G:X2}{color.B:X2}",
            };
            rich.Append($"<color=#{hex}>");
            // safety against TMP tags
            rich.Append(displayed.Replace("<", "‹").Replace(">", "›"));
            rich.Append("</color>");
            remaining -= displayed.Length;
            if (displayed.Length < text.Length)
            {
                rich.Append("...");
                break;
            }
        }

        return new ApConsoleMessage(message.ToString(), rich.ToString());
    }
}
