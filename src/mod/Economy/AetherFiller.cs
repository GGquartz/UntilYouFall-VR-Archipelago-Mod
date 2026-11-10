using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.SaveSystem;

namespace UntilYouFallArchipelago;

internal sealed class AetherFiller
{
    private readonly LogChannel _log;

    public static Func<LevelManager?> CurrentLevel { get; set; } = () => null;

    public AetherFiller(Action<string> log) => _log = log.Channel("APAether");

    public bool TryApply(int index, int amount)
    {
        var inventory = FindInventory();
        var currency = inventory?.currency;
        if (!AetherFillerRules.CanApply(connected: true, hasCurrency: currency != null))
        {
            _log.Write($"Aether index={index} waiting no active currency variable yet");
            return false;
        }

        var before = currency!.Value;
        var after = AetherFillerRules.Apply(before, amount);
        currency.Value = after;
        inventory!.saveManager?.SetEconomyDirty();
        inventory!.saveManager?.SaveEconomy();

        _log.Write($"Aether applied index={index} {before}->{after} (+{amount})");
        return true;
    }

    public static bool CanAfford(int price) => FindInventory()?.currency?.Value >= price;

    public static bool TrySpend(int price, Action commit, Action<string> log)
    {
        var inventory = FindInventory();
        var currency = inventory?.currency;
        if (currency == null || currency.Value < price)
        {
            log.Write(
                "APAether",
                $"Spend refused price={price} aether={(currency == null ? "missing" : currency.Value.ToString())}"
            );
            return false;
        }

        var before = currency.Value;
        currency.Value = before - price;
        try
        {
            inventory!.saveManager!.SetEconomyDirty();
            inventory.saveManager.SaveEconomy();
            commit();
        }
        catch (Exception failure)
        {
            currency.Value = before;
            try
            {
                inventory!.saveManager!.SetEconomyDirty();
                inventory.saveManager.SaveEconomy();
            }
            catch (Exception refundFailure)
            {
                log.Write("APAether", $"Refund save failed, restored in memory to {before}: {refundFailure}");
                throw new AggregateException(failure, refundFailure);
            }

            log.Write("APAether", $"Purchase failed, refunded to {before}: {failure.Message}");
            throw;
        }

        log.Write("APAether", $"Spent aether {before}->{currency.Value} (-{price})");
        return true;
    }

    private static ArmamentInventory? FindInventory()
    {
        var manager = CurrentLevel();
        if (manager == null || !manager.IsGameplayRoom())
            return null;

        var inventory = manager.armamentInventory;

        return inventory.HasProfile() && inventory.currency != null ? inventory : null;
    }
}
