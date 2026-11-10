namespace UntilYouFallArchipelago;

internal sealed class ItemEffects
{
    public delegate bool ApplyAetherEffect(int itemIndex, int amount);

    public Func<bool> CanHeal { get; init; } = () => false;
    public Action<int> Heal { get; init; } = _ => { };
    public ApplyAetherEffect ApplyAetherFiller { get; init; } = (_, _) => false;
    public Func<bool> CanSpawnEnemy { get; init; } = () => false;
    public Action? SpawnEnemy { get; init; }
    public Action? DodgeOnlyTrap { get; init; }
    public Action? ExhaustionTrap { get; init; }
    public Action? CursedTrap { get; init; }
    public Action? SuperCharge { get; init; }
    public Func<bool>? CanApplySuperCharge { get; init; }
    public Action? EnemyStasis { get; init; }
}

internal sealed partial class ArchipelagoClient
{
    public void ApplyNextItem(ItemEffects effects)
    {
        if (!Connected || _state == null || !_pendingItems.TryGetValue(HandledCount, out var item))
            return;
        if (ApItems.IsRunOnlyItem(item))
            ApplyRunOnlyEffect(item, effects);
        else if (item == ApItems.EliteEncounterTrapId)
            RecordEliteEncounterTrap();
        else if (TryApplyDurableItem(item))
            return;
        else if (TrapLabel(item) is { } trapLabel)
            ApplyTrap(trapLabel, TrapEffect(item, effects), effects);
        else if (ApItems.AetherBundleAmount(item) is int amount)
            ApplyAetherBundle(amount, effects);
        else if (item == ApItems.ShardOfVitalityId)
            ApplyVitality(effects);
        else
        {
            _log.Write($"Unsupported item {item} - disconnecting without use");
            Disconnect();
        }
    }

    private bool TryApplyDurableItem(long item)
    {
        if (!SynchronizeDurableItem(item, updateCounts: true))
            return false;

        if (DifficultySettings.IsItem(item) && !DifficultyConfig!.InRange((int)(item - DifficultySettings.FirstItemId)))
            _difficultyLog.Write($"Ignored item outside slot range: {item}");
        else if (item == ApItems.ProgressiveShopUnlockId)
            _shopLog.Write($"Progressive shop unlock received level={ShopUnlockCount}");

        _state!.CommitItem(HandledCount);
        _pendingItems.Remove(HandledCount - 1);

        return true;
    }

    private bool SynchronizeDurableItem(long item, bool updateCounts)
    {
        if (WeaponCatalog.ById(item) != null)
            _state!.UnlockWeapon(item);
        else if (ApItems.IsHand(item))
            _state!.UnlockHand(item);
        else if (DifficultySettings.IsItem(item))
        {
            if (DifficultyConfig!.InRange((int)(item - DifficultySettings.FirstItemId)))
                _state!.UnlockDifficulty(item);
        }
        else if (ApItems.IsTraitSlot(item))
            _state!.UnlockTraitSlot(item);
        else if (ApItems.IsWeaponAbility(item))
            _state!.UnlockWeaponAbility(item);
        else if (item == ApItems.ProgressiveWeaponUpgradeLevelId)
        {
            if (updateCounts)
                _state!.SetWeaponUpgradeItemCount(_receivedItemHistory.Count(item));
        }
        else if (item == ApItems.ProgressiveAetherMultiplierId)
        {
            if (updateCounts)
                _state!.SetAetherMultiplierItemCount(_receivedItemHistory.Count(item));
        }
        else if (ApItems.IsPlayerBonus(item))
        {
            if (updateCounts)
                _state!.SetPlayerBonusItemCount(item, _receivedItemHistory.Count(item));
        }
        else if (StartingTraitRules.MaxCount(item) > 0)
        {
            if (updateCounts)
                _state!.SetStartingTraitItemCount(item, _receivedItemHistory.Count(item));
        }
        else if (item != ApItems.ProgressiveShopUnlockId)
            return false;

        return true;
    }

    private bool CommitItem(int index, string label, Action? beforeCommit = null)
    {
        try
        {
            beforeCommit?.Invoke();
            _state!.CommitItem(index);
        }
        catch (Exception exception)
        {
            _log.Write($"Cannot persist {label} cursor, effect not applied: " + exception.Message);
            Disconnect();

            return false;
        }
        _pendingItems.Remove(index);

        return true;
    }

    private void ApplyRunOnlyEffect(long item, ItemEffects effects)
    {
        var index = HandledCount;
        var eligible =
            _runOnlyEffectReceipts.TryGetValue(index, out var inRun)
            && inRun
            && !_state!.VoidedRunOnlyEffects.Contains(index);
        var apply = item == ApItems.SuperChargeId ? effects.SuperCharge : effects.EnemyStasis;
        if (
            eligible
            && (
                apply == null
                || !effects.CanSpawnEnemy()
                || item == ApItems.SuperChargeId && effects.CanApplySuperCharge?.Invoke() == false
            )
        )
            return;
        if (!CommitItem(index, "run-only effect"))
            return;

        _runOnlyEffectReceipts.Remove(index);
        try
        {
            _log.Write($"Run effect applied index={index} item={item} receiptInRun={eligible}");
            if (eligible)
                apply!();
        }
        catch (Exception exception)
        {
            _log.Write($"Run effect failed after commit index={index} {exception.Message}");
            Disconnect();
        }
    }

    private void RecordEliteEncounterTrap()
    {
        var index = HandledCount;
        if (!CommitItem(index, ApItems.EliteEncounterTrapName, () => _state!.RegisterEliteEncounterTrap(index)))
            return;

        _log.Write($"Item recorded index={index} item=Elite Encounter Trap handled={HandledCount}");
    }

    private static string? TrapLabel(long item) =>
        item == ApItems.EnemySpawnTrapId ? ApItems.EnemySpawnTrapName
        : item == ApItems.DodgeOnlyTrapId ? ApItems.DodgeOnlyTrapName
        : item == ApItems.ExhaustionTrapId ? ApItems.ExhaustionTrapName
        : item == ApItems.CursedTrapId ? ApItems.CursedTrapName
        : null;

    private static Action? TrapEffect(long item, ItemEffects effects) =>
        item == ApItems.EnemySpawnTrapId ? effects.SpawnEnemy
        : item == ApItems.DodgeOnlyTrapId ? effects.DodgeOnlyTrap
        : item == ApItems.ExhaustionTrapId ? effects.ExhaustionTrap
        : effects.CursedTrap;

    private void ApplyTrap(string label, Action? apply, ItemEffects effects)
    {
        if (apply == null || !effects.CanSpawnEnemy())
            return;
        var index = HandledCount;
        if (!CommitItem(index, label))
            return;

        try
        {
            _log.Write($"Item consumed index={index} item={label} handled={HandledCount}");
            apply();
        }
        catch (Exception exception)
        {
            _log.Write($"{label} failed after committing index={index}: {exception.Message}");
            Disconnect();
        }
    }

    private void ApplyAetherBundle(int amount, ItemEffects effects)
    {
        var index = HandledCount;
        if (!effects.ApplyAetherFiller(index, amount))
            return;
        if (!CommitItem(index, "Aether filler"))
            return;

        _log.Write($"Item applied index={index} item={amount} Aether handled={HandledCount}");
    }

    private void ApplyVitality(ItemEffects effects)
    {
        if (!effects.CanHeal())
            return;
        var index = HandledCount;
        if (!CommitItem(index, ApItems.ShardOfVitalityName))
            return;

        try
        {
            effects.Heal(ApItems.VitalityHealAmount);
            _log.Write($"Item applied index={index} item=Shard of Vitality handled={HandledCount}");
        }
        catch (Exception exception)
        {
            _log.Write($"Shard of vitality failed after committing index={index}: {exception.Message}");
            Disconnect();
        }
    }
}
