using System.Reflection;
using UntilYouFallArchipelago;

internal static class ExtraTraitSlotTests
{
    public static void Run()
    {
        var ids = WeaponCatalog.All.Select(weapon => weapon.TraitSlotItemId).ToArray();
        Assert(
            ids.SequenceEqual(Enumerable.Range(910400, 15).Select(id => (long)id)),
            "trait-slot IDs are stable and follow weapon catalog order"
        );
        Assert(
            WeaponCatalog.All.All(weapon => WeaponCatalog.ByTraitSlotId(weapon.TraitSlotItemId)?.Id == weapon.Id),
            "every trait-slot item resolves to exactly one weapon"
        );
        Assert(
            ids.All(ApItems.IsTraitSlot) && !ApItems.IsTraitSlot(910019),
            "only extra trait-slot IDs use the durable trait-slot receipt path"
        );

        var mistcutter = WeaponCatalog.ById(910019)!;
        Assert(
            ExtraTraitSlotRules.Apply(2, mistcutter.Id, new[] { mistcutter.TraitSlotItemId }) == 3,
            "matching item adds one socket to the native result"
        );
        Assert(
            ExtraTraitSlotRules.Apply(2, mistcutter.Id, Array.Empty<long>()) == 2
                && ExtraTraitSlotRules.Apply(2, 910024, new[] { mistcutter.TraitSlotItemId }) == 2,
            "missing and other-weapon items leave native sockets unchanged"
        );

        var directory = Path.Combine(Path.GetTempPath(), "uyf-trait-slot-state-test-" + Guid.NewGuid());
        try
        {
            var state = ApState.Open(directory, "server/seed-A/team0/slot1");
            state.UnlockTraitSlot(mistcutter.TraitSlotItemId);
            state.UnlockTraitSlot(mistcutter.TraitSlotItemId);
            state.CommitItem(0);
            var reloaded = ApState.Open(directory, "server/seed-A/team0/slot1");
            Assert(
                reloaded.TraitSlotUnlocks.SetEquals(new[] { mistcutter.TraitSlotItemId }) && reloaded.HandledCount == 1,
                "trait-slot ownership persists idempotently with the item cursor"
            );
            Assert(
                ApState.Open(directory, "server/seed-B/team0/slot1").TraitSlotUnlocks.Count == 0,
                "trait-slot ownership is isolated by seed"
            );
            try
            {
                state.UnlockTraitSlot(910019);
                throw new Exception("non-trait-slot unlock accepted");
            }
            catch (ArgumentOutOfRangeException) { }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

        Console.WriteLine("PASS: per-weapon extra trait-slot IDs, additive rule and durable ownership");
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok)
            throw new Exception("Extra trait slots: " + message);
    }
}
