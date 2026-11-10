using Archipelago.MultiClient.Net.Models;
using UntilYouFallArchipelago;

internal static class ReceivedItemHistoryTests
{
    public static void Run()
    {
        var history = new ReceivedItemHistory();
        var countedItems = new[]
        {
            ApItems.ProgressiveWeaponUpgradeLevelId,
            ApItems.ProgressiveAetherMultiplierId,
            ApItems.ProgressiveShopUnlockId,
        }
            .Concat(PlayerBonusRules.ReceiptItemIds)
            .Concat(StartingTraitRules.All.Select(trait => trait.Id))
            .Concat(new[] { StartingTraitRules.ExtraPickId, StartingTraitRules.ExtraChoiceId })
            .ToArray();
        foreach (var item in countedItems)
        {
            Check(history.Record(0, item), "first receipt changes the count");
            Check(!history.Record(0, item) && history.Count(item) == 1, "duplicate receipt does not count twice");
            Check(history.Record(1, item) && history.Count(item) == 2, "distinct receipt counts another copy");
        }
        foreach (
            var item in new[] { ApItems.ShardOfVitalityId, ApItems.EnemySpawnTrapId, WeaponCatalog.All[0].Id, -1L }
        )
            Check(
                !history.Record(2, item) && history.Count(item) == 0,
                "non-counted items do not enter receipt counts"
            );

        history.Reset(preserveShopUnlocks: true);
        foreach (var item in countedItems)
            Check(
                history.Count(item) == (item == ApItems.ProgressiveShopUnlockId ? 2 : 0),
                "same-slot reconnect reset"
            );
        history.Reset(preserveShopUnlocks: false);
        Check(
            countedItems.All(item => history.Count(item) == 0),
            "new-slot or shortened-history reset clears all counts"
        );
        history.Record(0, ApItems.ProgressiveShopUnlockId);
        history.Record(1, ApItems.ProgressiveWeaponUpgradeLevelId);
        history.ResetShopUnlocks();
        Check(history.Count(ApItems.ProgressiveShopUnlockId) == 0, "full replay rebuilds shops from server history");
        Check(
            history.Count(ApItems.ProgressiveWeaponUpgradeLevelId) == 1,
            "full replay does not discard other receipt counts"
        );
        PacketReplay();
        Console.WriteLine(
            "PASS: receipt counts deduplicate all counted families and preserve reconnect/reset semantics"
        );
    }

    private static void PacketReplay()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-receipt-replay-" + Guid.NewGuid());
        var client = new ArchipelagoClient(directory, _ => { });
        var state = ApState.Open(directory, "receipt-replay");
        ClientSessionTestHelpers.Attach(client, state);
        var starter = WeaponCatalog.All[0];
        ClientSessionTestHelpers.SetProperty(client, "StartingWeaponIds", new[] { starter.Id });
        var trait = StartingTraitRules.All[0].Id;
        var ids = new[]
        {
            starter.Id,
            ApItems.RightHandId,
            starter.TraitSlotItemId,
            starter.SuperItemId,
            DifficultySettings.FirstItemId,
            ApItems.ProgressiveWeaponUpgradeLevelId,
            ApItems.ProgressiveWeaponUpgradeLevelId,
            ApItems.ProgressiveAetherMultiplierId,
            ApItems.ProgressiveShopUnlockId,
            ApItems.MaximumHealthBonusId,
            trait,
            ApItems.EnemyStasisId,
        };
        var packet = ids.Select(item => new NetworkItem { Item = item }).ToArray();
        var effects = 0;
        var handlers = new ItemEffects { CanSpawnEnemy = () => true, EnemyStasis = () => effects++ };
        try
        {
            client.HandleReceivedItems(0, packet, true);
            client.HandleReceivedItems(0, packet, true);
            Check(
                client.WeaponUpgradeItemCount == 2
                    && client.AetherMultiplierItemCount == 1
                    && client.ShopUnlockCount == 1
                    && client.PlayerBonusItemCounts[ApItems.MaximumHealthBonusId] == 1
                    && client.StartingTraitItemCounts[trait] == 1,
                "duplicate full packets keep durable counts unchanged"
            );
            Check(
                client.WeaponUnlocks.Contains(starter.Id)
                    && client.HandUnlocks.Contains(ApItems.RightHandId)
                    && client.TraitSlotUnlocks.Contains(starter.TraitSlotItemId)
                    && client.WeaponAbilityUnlocks.Contains(starter.SuperItemId)
                    && client.DifficultyUnlocks.Contains(DifficultySettings.FirstItemId),
                "packet receipt restores every permanent unlock family"
            );
            var path = Directory.GetFiles(directory, "*.json").Single();
            using (new BlockedSaveFile(path))
                client.HandleReceivedItems(0, packet, true);
            for (var index = 0; index < ids.Length; index++)
                client.ApplyNextItem(handlers);
            Check(
                client.HandledCount == ids.Length && effects == 1,
                "item processing commits each receipt and applies its effect once"
            );
            client.Disconnect();
            ClientSessionTestHelpers.Attach(client, ApState.Open(directory, state.Identity));
            client.HandleReceivedItems(0, packet, true);
            client.ApplyNextItem(handlers);
            Check(client.PendingItems == 0 && effects == 1, "reconnect cannot replay handled effects");

            state = ApState.Open(directory, state.Identity);
            ClientSessionTestHelpers.AttachState(client, state);
            client.HandleReceivedItems(0, [new NetworkItem { Item = ApItems.ProgressiveShopUnlockId }], false);
            Check(
                client.HandledCount == 0 && client.PendingItems == 1,
                "shorter server history resets the item cursor"
            );
            Check(
                client.WeaponUpgradeItemCount == 0
                    && client.AetherMultiplierItemCount == 0
                    && client.PlayerBonusItemCounts.Count == 0
                    && client.StartingTraitItemCounts.Count == 0
                    && client.ShopUnlockCount == 1,
                "shorter history discards old receipt counts before rebuilding"
            );
            Check(
                client.WeaponUnlocks.Contains(starter.Id)
                    && client.DifficultyUnlocks.Contains(DifficultySettings.FirstItemId),
                "shorter history restores starters"
            );
            var pending = (SortedDictionary<int, long>)ClientSessionTestHelpers.GetField(client, "_pendingItems");
            client.HandleReceivedItems(1, [new NetworkItem { Item = ApItems.EnemySpawnTrapId }], false);
            try
            {
                client.HandleReceivedItems(1, [new NetworkItem { Item = ApItems.ShardOfVitalityId }], false);
                throw new Exception("Conflicting pending history was accepted");
            }
            catch (InvalidDataException exception)
            {
                Check(
                    exception.Message == "Conflicting AP item history",
                    "conflicting receipt reports the expected error"
                );
                Check(pending[1] == ApItems.EnemySpawnTrapId, "conflict cannot replace the pending receipt");
            }
        }
        finally
        {
            client.Disconnect();
            Directory.Delete(directory, true);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception("Received item history: " + message);
    }
}
