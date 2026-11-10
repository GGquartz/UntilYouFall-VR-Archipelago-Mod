using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;

namespace UntilYouFallArchipelago;

internal sealed record StageRewardItemInfo(
    long LocationId,
    string ItemName,
    string PlayerAlias,
    string Game,
    ItemFlags Flags,
    bool IsLocalPlayer
)
{
    public static StageRewardItemInfo From(ScoutedItemInfo info) =>
        new(
            info.LocationId,
            info.ItemDisplayName,
            info.Player.Alias,
            info.ItemGame,
            info.Flags,
            info.IsReceiverRelatedToActivePlayer
        );
}
