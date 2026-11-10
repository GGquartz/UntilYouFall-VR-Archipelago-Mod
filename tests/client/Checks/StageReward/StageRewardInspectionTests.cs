using System.Collections.ObjectModel;
using Archipelago.MultiClient.Net.DataPackage;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using UntilYouFallArchipelago;

internal static class StageRewardInspectionTests
{
    public static void Run()
    {
        var local = Info(ItemFlags.Advancement, true);
        Assert(
            StageRewardInspection.Title(local) == "Estus Shard"
                && StageRewardInspection.Description(local) == "It seems important..."
                && StageRewardInspection.Color(local) == "AF99EF",
            "local progression item uses its name, description and AP color"
        );

        var remote = Info(ItemFlags.NeverExclude, false);
        Assert(
            StageRewardInspection.Title(remote) == "DS3Test's Estus Shard"
                && StageRewardInspection.Description(remote)
                    == "An item from a world called Dark Souls III. It seems useful..."
                && StageRewardInspection.Color(remote) == "6D8BE8",
            "remote useful item includes receiver and game"
        );
        Assert(
            StageRewardInspection.Description(Info(ItemFlags.None, true)) == "It does not seem important..."
                && StageRewardInspection.Description(Info(ItemFlags.Trap, true)) == "It seems dangerous...",
            "normal and trap descriptions are distinct"
        );
        Assert(
            StageRewardInspection.Title(
                new StageRewardItemInfo(911401, "Item\nName", "Player", "Game\nName", ItemFlags.None, false)
            ) == "Player's Item Name",
            "inspection text keeps names on one line"
        );
        var active = new PlayerInfo(
            1,
            1,
            "Me",
            "Myself",
            "Until You Fall",
            Array.Empty<NetworkSlot>(),
            Array.Empty<int>()
        );
        var players = new TestPlayers(active);
        var scouted = new ScoutedItemInfo(
            new NetworkItem
            {
                Item = 123,
                Location = 911401,
                Player = 1,
                Flags = ItemFlags.Advancement,
            },
            "Until You Fall",
            "Until You Fall",
            new TestResolver(),
            players,
            active
        );
        var converted = StageRewardItemInfo.From(scouted);
        Assert(
            converted.LocationId == 911401
                && converted.ItemName == "Resolved Item"
                && converted.PlayerAlias == "Myself"
                && converted.Game == "Until You Fall"
                && converted.Flags == ItemFlags.Advancement
                && converted.IsLocalPlayer,
            "full ScoutedItemInfo converts at the network boundary"
        );
        Console.WriteLine("PASS: Stage Reward inspection titles, descriptions, escaping and AP colors");
    }

    private static StageRewardItemInfo Info(ItemFlags flags, bool local) =>
        new(911401, "Estus Shard", "DS3Test", "Dark Souls III", flags, local);

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception("Stage Reward inspection: " + message);
    }

    private sealed class TestResolver : IItemInfoResolver
    {
        public string GetItemName(long itemId, string game) => "Resolved Item";

        public string GetLocationName(long locationId, string game) => "Stage Reward 1";

        public long GetLocationId(string locationName, string game) => 911401;
    }

    private sealed class TestPlayers : IPlayerHelper
    {
        public TestPlayers(PlayerInfo active) => ActivePlayer = active;

        public ReadOnlyDictionary<int, ReadOnlyCollection<PlayerInfo>> Players { get; } =
            new(new Dictionary<int, ReadOnlyCollection<PlayerInfo>>());
        public IEnumerable<PlayerInfo> AllPlayers => new[] { ActivePlayer! };
        public PlayerInfo ActivePlayer { get; }

        public PlayerInfo GetPlayerInfo(int team, int slot) => slot == ActivePlayer.Slot ? ActivePlayer : null!;

        public PlayerInfo GetPlayerInfo(int slot) => slot == ActivePlayer.Slot ? ActivePlayer : null!;

        public string GetPlayerName(int slot) => GetPlayerInfo(slot)?.Name ?? "Unknown";

        public string GetPlayerAlias(int slot) => GetPlayerInfo(slot)?.Alias ?? "Unknown";

        public string GetPlayerAliasAndName(int slot) => GetPlayerInfo(slot)?.Alias ?? "Unknown";
    }
}
