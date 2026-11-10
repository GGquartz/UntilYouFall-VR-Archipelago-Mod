namespace UntilYouFallArchipelago;

internal sealed class ReceivedItemHistory
{
    private readonly Dictionary<long, HashSet<int>> _receipts = [];

    public bool Record(int index, long item)
    {
        if (
            item != ApItems.ProgressiveWeaponUpgradeLevelId
            && item != ApItems.ProgressiveAetherMultiplierId
            && item != ApItems.ProgressiveShopUnlockId
            && !ApItems.IsPlayerBonus(item)
            && StartingTraitRules.MaxCount(item) == 0
        )
            return false;

        if (!_receipts.TryGetValue(item, out var indices))
            _receipts[item] = indices = [];

        return indices.Add(index);
    }

    public int Count(long item) => _receipts.TryGetValue(item, out var indices) ? indices.Count : 0;

    public void Reset(bool preserveShopUnlocks)
    {
        _receipts.TryGetValue(ApItems.ProgressiveShopUnlockId, out var shops);
        _receipts.Clear();
        if (preserveShopUnlocks && shops != null)
            _receipts[ApItems.ProgressiveShopUnlockId] = shops;
    }

    public void ResetShopUnlocks() => _receipts.Remove(ApItems.ProgressiveShopUnlockId);
}
