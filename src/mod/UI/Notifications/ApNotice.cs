using Archipelago.MultiClient.Net.Enums;

namespace UntilYouFallArchipelago;

internal sealed record ApNotice(
    string PlainText,
    string RichText,
    bool HasLocation = false,
    bool Death = false,
    bool Trap = false
)
{
    public bool IsUrgent => Death || Trap;

    public static string ItemColor(ItemFlags flags) =>
        (flags & ItemFlags.Advancement) != 0 ? ApColors.Progression
        : (flags & ItemFlags.NeverExclude) != 0 ? ApColors.Useful
        : (flags & ItemFlags.Trap) != 0 ? ApColors.Salmon
        : ApColors.Filler;

    public static ApNotice Plain(string message) => new(message, message);

    public static ApNotice Error(string message) => new(message, ApColors.Rich(ApColors.Salmon, message));

    private static string Safe(string text) => text.Replace("\r", " ").Replace("\n", " ");

    private static string Item(string name, ItemFlags flags) => $"<color=#{ItemColor(flags)}>{Safe(name)}</color>";

    private static string Player(string name) => ApColors.Rich(ApColors.Player, Safe(name));

    public static ApNotice Received(string item, ItemFlags flags, string sender) =>
        new(
            $"Received {item} from {sender}",
            $"Received {Item(item, flags)} from {Player(sender)}",
            Trap: (flags & ItemFlags.Trap) != 0
        );

    public static ApNotice Sent(string item, ItemFlags flags, string receiver) =>
        new($"Sent {item} to {receiver}", $"Sent {Item(item, flags)} to {Player(receiver)}");

    public static ApNotice Found(string item, ItemFlags flags) =>
        new(
            $"Found your {item}",
            $"Found {ApColors.Rich(ApColors.Magenta, "your")} {Item(item, flags)}",
            Trap: (flags & ItemFlags.Trap) != 0
        );

    public static ApNotice DeathLink(string source, string cause, string? trap = null)
    {
        var text = string.IsNullOrWhiteSpace(cause) ? $"{source} died" : cause;
        return new(
            $"DeathLink: {text}{(trap == null ? "" : $"\nReceived {trap}")}",
            $"{ApColors.Rich(ApColors.Salmon, "DeathLink:")} {Safe(text)}{(trap == null ? "" : $"\nReceived {Item(trap, ItemFlags.Trap)}")}",
            Death: true
        );
    }

    public ApNotice AtLocation(string location) =>
        this with
        {
            PlainText = $"{location}\n{PlainText}",
            RichText = $"<size=80%><b>{ApColors.Rich(ApColors.Location, Safe(location))}</b></size>\n{RichText}",
            HasLocation = true,
        };
}
