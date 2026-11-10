using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.SaveSystem;
using UnityEngine;
using UntilYouFallArchipelago;

internal static class AetherPurchaseTests
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-shop-test-" + Guid.NewGuid());
        var inventory = new ArmamentInventory
        {
            currency = new Currency { Value = 150 },
            saveManager = new SaveManager(),
        };
        var savedBalance = 150;
        inventory.saveManager.OnSave = () => savedBalance = inventory.currency.Value;
        var title = new Room { Pointer = (IntPtr)1 };
        var manager = new LevelManager
        {
            armamentInventory = inventory,
            titleRoom = title,
            CurrentLevel = new Level { Room = title },
        };
        Resources.Objects = [inventory, manager];
        AetherFiller.CurrentLevel = () => manager;
        try
        {
            var filler = new AetherFiller(_ => { });
            var menuCommit = false;
            Check(!AetherFiller.CanAfford(100), "profile preview is not a payable inventory");
            Check(!filler.TryApply(0, 100), "filler waits while profile previews are loaded");
            Check(!AetherFiller.TrySpend(100, () => menuCommit = true, _ => { }), "preview cannot spend");
            Check(
                !menuCommit && inventory.currency.Value == 150 && savedBalance == 150,
                "menu leaves currency and native save untouched"
            );
            manager.CurrentLevel = new Level { Room = new Room { Pointer = (IntPtr)2 } };
            manager.IsLoading = true;
            Check(!filler.TryApply(0, 100), "filler waits during level loading");
            manager.IsLoading = false;
            manager.IsCoreLoading = true;
            Check(!filler.TryApply(0, 100), "filler waits during core loading");
            manager.IsCoreLoading = false;
            inventory.saveManager.ProfileIndex = -1;
            Check(!filler.TryApply(0, 100), "filler waits for a selected native profile");
            inventory.saveManager.ProfileIndex = 1;
            Check(AetherFiller.CanAfford(100), "affordable item");
            Check(!AetherFiller.CanAfford(151), "unaffordable item");
            inventory.currency.Value = 100;
            Check(AetherFiller.CanAfford(100), "exact price allowed");
            inventory.currency.Value = 99;
            Check(!AetherFiller.CanAfford(100), "gate sees balance change");
            Check(
                !AetherFiller.TrySpend(100, () => throw new Exception("must not commit"), _ => { }),
                "insufficient funds rejected"
            );
            Check(inventory.currency.Value == 99 && savedBalance == 150, "rejection does not spend or save");
            inventory.currency.Value = 150;
            var state = ApState.Open(directory, "purchase-test");
            state.RecordCheck(911602);
            var locked = new BlockedSaveFile(Directory.GetFiles(directory, "*.json").Single());
            Throws(() => AetherFiller.TrySpend(100, () => state.RecordCheck(911601), _ => { }));
            Check(!state.Checks.Contains(911601), "failed AP save rolls back check");
            Check(inventory.currency.Value == 150 && savedBalance == 150, "failed AP save refunds currency");
            locked.Dispose();
            Check(AetherFiller.TrySpend(100, () => state.RecordCheck(911601), _ => { }), "retry succeeds");
            Check(savedBalance == 50 && inventory.currency.Value == 50, "success spends exactly once");
            Check(ApState.Open(directory, "purchase-test").Checks.Contains(911601), "check persisted");
            inventory.currency.Value = 150;
            inventory.saveManager.OnSave = () => throw new IOException("economy save failed");
            var committed = false;
            Throws(() => AetherFiller.TrySpend(100, () => committed = true, _ => { }));
            Check(
                !committed && inventory.currency.Value == 150,
                "failed economy save restores memory and never commits"
            );
            Resources.Objects = Array.Empty<object>();
            AetherFiller.CurrentLevel = () => null;
            Check(!AetherFiller.CanAfford(100), "missing inventory blocks crush");
            Check(!AetherFiller.TrySpend(100, () => committed = true, _ => { }), "missing inventory rejects purchase");
        }
        finally
        {
            Resources.Objects = Array.Empty<object>();
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        Console.WriteLine("PASS: shop affordability, payment, AP save rollback and retry");
    }

    private static void Throws(Action action)
    {
        try
        {
            action();
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        catch (AggregateException)
        {
            return;
        }
        throw new Exception("Expected save failure");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
