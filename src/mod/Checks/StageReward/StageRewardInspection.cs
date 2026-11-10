using Archipelago.MultiClient.Net.Enums;

namespace UntilYouFallArchipelago;

internal static class StageRewardInspection
{
    public static string Title(StageRewardItemInfo info) =>
        info.IsLocalPlayer ? Safe(info.ItemName) : $"{Safe(info.PlayerAlias)}'s {Safe(info.ItemName)}";

    public static string Description(StageRewardItemInfo info)
    {
        var prefix = info.IsLocalPlayer ? "" : $"An item from a world called {Safe(info.Game)}. ";
        var description =
            (info.Flags & ItemFlags.Advancement) != 0 ? "It seems important..."
            : (info.Flags & ItemFlags.NeverExclude) != 0 ? "It seems useful..."
            : (info.Flags & ItemFlags.Trap) != 0 ? "It seems dangerous..."
            : "It does not seem important...";

        return prefix + description;
    }

    public static string Color(StageRewardItemInfo info) => ApNotice.ItemColor(info.Flags);

    public static string Safe(string text) => text.Replace("\r", " ").Replace("\n", " ");
}
