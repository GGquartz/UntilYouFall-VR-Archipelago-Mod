using UntilYouFallArchipelago;

internal static class ApStateTests
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-ap-state-test-" + Guid.NewGuid());
        try
        {
            var state = ApState.Open(directory, "server/seed-A/team0/slot1");
            Assert(state.HandledCount == 0, "new slot starts at zero");
            var firstStageWeaponCheck = StageChecks.LocationId(1, 910012);
            state.RecordCheck(firstStageWeaponCheck);
            state.RecordCheck(firstStageWeaponCheck);
            Assert(state.Checks.Count == 1, "duplicate check stored only once");
            state.CommitItem(0);
            Assert(state.HandledCount == 1, "first item advances cursor");
            var reloaded = ApState.Open(directory, "server/seed-A/team0/slot1");
            Assert(
                reloaded.HandledCount == 1 && reloaded.Checks.Contains(firstStageWeaponCheck),
                "restart restores item cursor and offline check"
            );
            reloaded.CommitItem(1);
            Assert(reloaded.HandledCount == 2, "second identical item has a distinct index");
            var rejected = false;
            try
            {
                reloaded.CommitItem(3);
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
            Assert(rejected && reloaded.HandledCount == 2, "missing item index cannot be skipped");
            var otherSeed = ApState.Open(directory, "server/seed-B/team0/slot1");
            Assert(otherSeed.HandledCount == 0 && otherSeed.Checks.Count == 0, "different seed starts clean");
            var otherSlot = ApState.Open(directory, "server/seed-A/team0/slot2");
            Assert(otherSlot.HandledCount == 0, "different slot starts clean");
            reloaded.UnlockWeapon(910019);
            reloaded.UnlockWeapon(910019);
            var weaponState = ApState.Open(directory, "server/seed-A/team0/slot1");
            Assert(
                weaponState.WeaponUnlocks.SetEquals(new[] { 910019L })
                    && weaponState.HandledCount == reloaded.HandledCount,
                "weapon unlock persists idempotently without advancing a deferred consumable cursor"
            );
            Assert(
                otherSeed.WeaponUnlocks.Count == 0 && otherSlot.WeaponUnlocks.Count == 0,
                "weapon ownership is isolated by seed and slot"
            );
            reloaded.SetWeaponUpgradeItemCount(1);
            reloaded.SetWeaponUpgradeItemCount(1);
            Assert(
                ApState.Open(directory, "server/seed-A/team0/slot1").WeaponUpgradeItemCount == 1,
                "progressive weapon upgrade count persists idempotently"
            );
            reloaded.SetWeaponUpgradeItemCount(9);
            reloaded.SetWeaponUpgradeItemCount(12);
            Assert(
                ApState.Open(directory, "server/seed-A/team0/slot1").WeaponUpgradeItemCount == 9,
                "progressive weapon upgrade count is monotonic and capped at nine"
            );
            Assert(
                otherSeed.WeaponUpgradeItemCount == 0 && otherSlot.WeaponUpgradeItemCount == 0,
                "weapon upgrade cap is isolated by seed and slot"
            );
            var notified = ApState.Open(directory, "server/notify/team0/slot1");
            notified.RecordReceivedNotice(0);
            Assert(
                notified.NotifiedReceivedCount == 1 && notified.HandledCount == 0,
                "notification cursor is independent of deferred healing"
            );
            var noticeReload = ApState.Open(directory, "server/notify/team0/slot1");
            Assert(noticeReload.NotifiedReceivedCount == 1, "notification cursor survives reconnect");
            noticeReload.RecordReceivedNotice(0);
            Assert(noticeReload.NotifiedReceivedCount == 1, "duplicate receipt cannot advance notification cursor");
            var legacyIdentity = "server/legacy/team0/slot1";
            var legacyHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(legacyIdentity))
            );
            File.WriteAllText(
                Path.Combine(directory, legacyHash + ".json"),
                System.Text.Json.JsonSerializer.Serialize(
                    new
                    {
                        Identity = legacyIdentity,
                        HandledCount = 3,
                        Checks = new long[0],
                    }
                )
            );
            Assert(
                ApState.Open(directory, legacyIdentity).NotifiedReceivedCount == 3,
                "old state does not announce previously consumed rewards"
            );
            var resetIdentity = "server/reset/team0/slot1";
            var resetState = ApState.Open(directory, resetIdentity);
            resetState.UnlockWeapon(910019);
            resetState.UnlockHand(910003);
            resetState.SetWeaponUpgradeItemCount(2);
            resetState.SetAetherMultiplierItemCount(1);
            resetState.SetPlayerBonusItemCount(ApItems.MaximumHealthBonusId, 2);
            resetState.CommitItem(0);
            resetState.CommitItem(1);
            resetState.CommitItem(2);
            resetState.RecordReceivedNotice(0);
            resetState.RecordReceivedNotice(1);
            resetState.RecordReceivedNotice(2);
            resetState.RecordCheck(StageChecks.LocationId(1, 910012));
            resetState.ResetItemHistory();
            Assert(
                resetState.HandledCount == 0
                    && resetState.NotifiedReceivedCount == 0
                    && resetState.WeaponUnlocks.Count == 0
                    && resetState.HandUnlocks.Count == 0
                    && resetState.TraitSlotUnlocks.Count == 0
                    && resetState.WeaponUpgradeItemCount == 0
                    && resetState.AetherMultiplierItemCount == 0
                    && resetState.PlayerBonusItemCounts.Count == 0
                    && resetState.Checks.Count == 1,
                "ResetItemHistory clears item cursors and unlocks while preserving offline checks"
            );
            var reloadedReset = ApState.Open(directory, resetIdentity);
            Assert(
                reloadedReset.HandledCount == 0
                    && reloadedReset.WeaponUpgradeItemCount == 0
                    && reloadedReset.WeaponUnlocks.Count == 0
                    && reloadedReset.Checks.Count == 1,
                "reset state persists across reload"
            );
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        Console.WriteLine(
            "PASS: persistence, duplicate checks, seed/slot isolation, notifications, item cursors and reset"
        );
        Console.WriteLine("");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
