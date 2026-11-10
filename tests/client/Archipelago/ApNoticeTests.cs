using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using UntilYouFallArchipelago;

internal static class ApNoticeTests
{
    public static void Run()
    {
        Assert(ApNotice.ItemColor(ItemFlags.None) == "00EEEE", "normal uses AP cyan");
        Assert(ApNotice.ItemColor(ItemFlags.Advancement) == "AF99EF", "progression uses AP plum");
        Assert(ApNotice.ItemColor(ItemFlags.NeverExclude) == "6D8BE8", "useful uses AP slateblue");
        Assert(ApNotice.ItemColor(ItemFlags.Trap) == "FA8072", "trap uses AP salmon");
        Assert(
            ApNotice.ItemColor(ItemFlags.Advancement | ItemFlags.NeverExclude | ItemFlags.Trap) == "AF99EF",
            "progression wins combined flags"
        );
        Assert(ApNotice.ItemColor(ItemFlags.NeverExclude | ItemFlags.Trap) == "6D8BE8", "useful wins over trap");
        foreach (
            var flags in new[]
            {
                ItemFlags.Trap,
                ItemFlags.Advancement | ItemFlags.Trap,
                ItemFlags.NeverExclude | ItemFlags.Trap,
                ItemFlags.Advancement | ItemFlags.NeverExclude | ItemFlags.Trap,
            }
        )
        {
            Assert(
                ApNotice.Received("Trap", flags, "Player2").Trap,
                "every received trap flag selects the negative sound regardless of color priority"
            );
            Assert(
                ApNotice.Found("Trap", flags).AtLocation("Clear Stage 1").Trap,
                "an own-location trap retains its sound classification"
            );
            Assert(
                !ApNotice.Sent("Trap", flags, "Player2").AtLocation("Clear Stage 1").Trap,
                "sending a trap to someone else keeps the normal sound"
            );
        }
        foreach (
            var flags in new[]
            {
                ItemFlags.None,
                ItemFlags.Advancement,
                ItemFlags.NeverExclude,
                ItemFlags.Advancement | ItemFlags.NeverExclude,
            }
        )
            Assert(
                !ApNotice.Received("Item", flags, "Player2").Trap && !ApNotice.Found("Item", flags).Trap,
                "non-trap receipts keep the normal sound"
            );
        var deathNotice = ApNotice.DeathLink("Player2", "Player2 died", "Cursed Trap");
        Assert(deathNotice.Death && !deathNotice.Trap, "DeathLink preserves its separate sound even with a trap");
        var multiline = ApNotice.Received("Heal\nPotion", ItemFlags.None, "Player\r\n2");
        Assert(
            !multiline.RichText.Contains('\n') && !multiline.RichText.Contains('\r'),
            "remote names cannot add extra lines"
        );
        var located = ApNotice.Found("Shard of Vitality", ItemFlags.None).AtLocation("Clear Stage 1");
        Assert(
            located.HasLocation && located.PlainText == "Clear Stage 1\nFound your Shard of Vitality",
            "own check includes its location before the reward"
        );
        Assert(
            !ApNotice.Received("Shard of Vitality", ItemFlags.None, "Player2").HasLocation,
            "remote receipt keeps the compact layout"
        );
        var multilineLocation = ApNotice
            .Sent("Shard of Vitality", ItemFlags.None, "Player2")
            .AtLocation("Clear\nStage 1");
        Assert(multilineLocation.RichText.Count(c => c == '\n') == 1, "location names cannot add additional lines");
        ClientDispatch();
        MissingNotificationCallback();
        SentNotificationDeduplication();
        Console.WriteLine("PASS: AP notice colors, sounds, line sanitising, replay and durable delivery");
    }

    private static void ClientDispatch()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-notice-dispatch-" + Guid.NewGuid());
        var state = ApState.Open(directory, "notice-dispatch");
        var notices = new List<ApNotice>();
        var logs = new List<string>();
        var client = new ArchipelagoClient(
            directory,
            logs.Add,
            notice =>
            {
                Assert(
                    ApState.Open(directory, state.Identity).NotifiedReceivedCount == notices.Count + 4,
                    "notification cursor saves before delivery"
                );
                notices.Add(notice);
            }
        );
        ClientSessionTestHelpers.Attach(client, state);
        var packet = new[]
        {
            new NetworkItem { Item = WeaponCatalog.All[0].Id, Location = -2 },
            new NetworkItem { Item = ApItems.RightHandId, Location = -2 },
            new NetworkItem { Item = DifficultySettings.FirstItemId, Location = -2 },
            new NetworkItem
            {
                Item = ApItems.ShardOfVitalityId,
                Location = 123,
                Player = 1,
            },
            new NetworkItem
            {
                Item = ApItems.EnemySpawnTrapId,
                Location = 456,
                Player = 2,
                Flags = ItemFlags.Trap,
            },
            new NetworkItem
            {
                Item = ApItems.ProgressiveWeaponUpgradeLevelId,
                Location = -2,
                Player = 0,
            },
        };
        try
        {
            client.HandleReceivedItems(0, packet, false);
            client.Update();
            Assert(
                notices.Count == 3 && state.NotifiedReceivedCount == packet.Length,
                "starting weapons, hands and difficulties are silent but still advance the cursor"
            );
            Assert(
                notices[0].PlainText
                    == ApNotice
                        .Found($"Item {ApItems.ShardOfVitalityId}", ItemFlags.None)
                        .AtLocation("Location 123")
                        .PlainText,
                "self-found receipt includes its location"
            );
            Assert(
                notices[1].PlainText
                    == ApNotice.Received($"Item {ApItems.EnemySpawnTrapId}", ItemFlags.Trap, "Player2").PlainText
                    && notices[1].Trap,
                "foreign trap keeps its sender and trap flag"
            );
            Assert(
                notices[2].PlainText
                    == ApNotice
                        .Received($"Item {ApItems.ProgressiveWeaponUpgradeLevelId}", ItemFlags.None, "Server")
                        .PlainText,
                "server receipts are not treated as silent weapon starters"
            );
            client.HandleReceivedItems(0, packet, false);
            client.Update();
            Assert(notices.Count == 3, "duplicate history cannot repeat delivered notifications");
            client.Disconnect();
            state = ApState.Open(directory, state.Identity);
            ClientSessionTestHelpers.Attach(client, state);
            client.HandleReceivedItems(0, packet, false);
            client.Update();
            Assert(notices.Count == 3, "reconnect retains the saved notification cursor");

            client.HandleReceivedItems(
                packet.Length,
                [new NetworkItem { Item = ApItems.ShardOfVitalityId, Player = 2 }],
                false
            );
            var path = Directory.GetFiles(directory, "*.json").Single();
            using (new BlockedSaveFile(path))
                client.Update();
            Assert(
                !client.Connected && notices.Count == 3 && state.NotifiedReceivedCount == packet.Length,
                "save failure disconnects without delivering or consuming the notice"
            );
            Assert(
                logs.Any(log => log.Contains("Cannot persist notification cursor:")),
                "cursor failure retains its support log"
            );
        }
        finally
        {
            client.Disconnect();
            Directory.Delete(directory, true);
        }
    }

    private static void SentNotificationDeduplication()
    {
        var notices = new List<ApNotice>();
        var notifications = new ClientNotifications(_ => { }, notices.Add);
        var notice = ApNotice.Sent("test item", ItemFlags.None, "Player2");
        notifications.NotifySent(123, notice);
        notifications.NotifySent(123, notice);
        Assert(notices.Count == 1, "sent notices deduplicate by location");
        notifications.ClearReceived();
        notifications.NotifySent(123, notice);
        Assert(notices.Count == 1, "disconnect cleanup retains sent notice deduplication");
        notifications.Reset();
        notifications.NotifySent(123, notice);
        Assert(notices.Count == 2, "validated login resets sent notice deduplication");
    }

    private static void MissingNotificationCallback()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-notice-no-callback-" + Guid.NewGuid());
        var state = ApState.Open(directory, "notice-no-callback");
        var logs = new List<string>();
        var client = new ArchipelagoClient(directory, logs.Add);
        ClientSessionTestHelpers.Attach(client, state);
        try
        {
            client.HandleReceivedItems(0, [new NetworkItem { Item = ApItems.ShardOfVitalityId }], false);
            client.Update();
            Assert(state.NotifiedReceivedCount == 0, "without a UI callback received notices remain pending");
            Assert(!logs.Any(log => log.StartsWith("[APNotice]")), "pending notices are not logged as delivered");
            typeof(ArchipelagoClient)
                .GetMethod(
                    "Notify",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
                )!
                .Invoke(client, [ApNotice.Found("test notice", ItemFlags.None)]);
            Assert(logs.Any(log => log.Contains("Found your test notice")), "direct notices still log without a UI");
        }
        finally
        {
            client.Disconnect();
            Directory.Delete(directory, true);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
