using System.Reflection;
using Archipelago.MultiClient.Net.Colors;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.MessageLog.Parts;
using Archipelago.MultiClient.Net.Models;
using UntilYouFallArchipelago;

internal static class ApConsoleTests
{
    public static void Run()
    {
        var message = CreateMessage(
            Part("Player2", PaletteColor.Magenta),
            Part(" found ", null),
            Part("Mistcutter", PaletteColor.Plum)
        );
        var entry = ApConsoleMessage.FromLog(message);
        Check(entry.PlainText == "Player2 found Mistcutter", "full foreign player message");
        Check(entry.RichText.Contains("<color=#AF99EF>Mistcutter</color>"), "AP progression color: " + entry.RichText);
        var unsafeText = ApConsoleMessage.FromLog(CreateMessage(Part("<size=99>Hello</size>\nChat", null)));
        Check(
            !unsafeText.RichText.Contains("<size=") && !unsafeText.RichText.Contains('\n'),
            "chat cannot inject TMP tags or extra rows"
        );
        var longText = ApConsoleMessage.FromLog(
            CreateMessage(Part(new string('x', 158), null), Part("Mistcutter", PaletteColor.Plum))
        );
        Check(
            longText.PlainText.Length == 168 && longText.RichText.EndsWith("</color>..."),
            "display truncation preserves full plain text and closes color tags"
        );
        var buffer = new ApConsoleBuffer();
        for (var i = 1; i <= 12; i++)
            buffer.Add(new ApConsoleMessage($"message{i}", $"message{i}"));
        Check(
            buffer.RichText.Split('\n').Length == 12,
            "message count does not discard content before the renderer measures its height"
        );
        for (var i = 1; i <= 10; i++)
            Check(buffer.DropOldest(), "renderer can free space");
        Check(buffer.RichText == "message11\nmessage12", "height trimming retains newest entries in order");
        Check(
            buffer.DropOldest() && !buffer.DropOldest() && buffer.RichText == "message12",
            "trimming never removes the sole newest message"
        );
    }

    // SDK constructors are internal; create real 6.7.1 message objects for isolated renderer tests.
    private static MessagePart Part(string text, PaletteColor? color) =>
        (MessagePart)
            Activator.CreateInstance(
                typeof(MessagePart),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                [MessagePartType.Text, new JsonMessagePart { Text = text }, color],
                null
            )!;

    private static LogMessage CreateMessage(params MessagePart[] parts) =>
        (LogMessage)
            Activator.CreateInstance(
                typeof(LogMessage),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                [parts],
                null
            )!;

    private static void Check(bool condition, string description)
    {
        if (!condition)
            throw new Exception("AP console: " + description);
    }
}
