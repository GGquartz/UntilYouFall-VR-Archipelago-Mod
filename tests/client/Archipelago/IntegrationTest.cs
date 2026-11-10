using UntilYouFallArchipelago;

internal static class IntegrationTest
{
    public static async Task Run(string server)
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-ap-network-test-" + Guid.NewGuid());
        var notices = new List<ApNotice>();
        var logs = new List<string>();
        void Log(string message)
        {
            Console.WriteLine(message);
            logs.Add(message);
        }
        var client = new ArchipelagoClient(directory, Log, notices.Add);
        var duelist = WeaponCatalog.ById(910024)!;
        var mistcutter = WeaponCatalog.ById(910019)!;
        var effects = 0;
        try
        {
            client.FirstKill(910301);
            client.Connect(server, "UYFTest", "");
            await Until(() => client.Connected, client);
            await PumpFor(client, 300);
            Assert(client.ConfirmedChecks == 0, "Integration test needs a fresh unsaved server slot.");
            Assert(
                client.StartingWeaponIds.SequenceEqual(new[] { 910024L }) && client.WeaponUnlocks.Contains(910024),
                "AP slot selects Duelist's Edge as its single starting weapon"
            );
            Assert(client.StartingHands == StartingHands.RightOnly, "AP slot selects the configured starting hand");
            Assert(notices.Count == 0, "starting weapons unlock silently without popup notifications");
            while (client.PendingItems > 0)
            {
                var before = client.HandledCount;
                client.ApplyNextItem(
                    new ItemEffects
                    {
                        CanHeal = () => false,
                        Heal = Heal,
                        ApplyAetherFiller = (_, __) => false,
                        CanSpawnEnemy = () => false,
                        SpawnEnemy = () =>
                            throw new Exception("Unexpected Enemy Spawn Trap in the Vitality network test."),
                    }
                );
                if (client.HandledCount == before)
                    break;
            }
            Assert(
                client.HandledCount >= 1 && effects == 0,
                "precollected weapon and hand items advance without healing"
            );

            client.Disconnect();
            client.Connect(server, "UYFTest", "");
            Assert(
                client.StartingWeaponIds.SequenceEqual(new[] { 910024L }),
                "pending login retains the last validated starter"
            );
            await Until(() => client.Connected && client.StartingWeaponIds.SequenceEqual(new[] { 910024L }), client);
            await PumpFor(client, 300);
            Assert(
                client.PendingItems == 0 && notices.Count == 0,
                "starter reconnect restores ownership without replay"
            );

            client.ClearStage(1, new[] { duelist });
            await Until(() => client.ConfirmedChecks == 1, client);
            Assert(
                notices.Count == 1
                    && notices[^1].HasLocation
                    && notices[^1].PlainText.StartsWith("Clear Stage 1 with Duelist's Edge\n"),
                "own check is announced once with its actual location"
            );
            client.ClearStage(1, new[] { duelist });
            await PumpFor(client, 300);
            Assert(client.ConfirmedChecks == 1 && notices.Count == 1, "duplicate clear does not add a reward");

            client.Disconnect();
            client.ClearStage(2, new[] { duelist });
            client.ClearStage(2, new[] { duelist });
            await Task.Delay(300);
            client = new ArchipelagoClient(directory, Log, notices.Add);
            client.Connect(server, "UYFTest", "");
            await Until(() => client.Connected && client.ConfirmedChecks == 2, client);
            Assert(notices.Count == 2, "offline clear survives recreation without replaying prior notices");
            client.ClearStage(3, new[] { duelist, mistcutter });
            client.ClearStage(3, new[] { duelist, mistcutter });
            await Until(() => client.ConfirmedChecks == 4, client);
            Assert(
                notices.Count == 4
                    && notices.Any(notice => notice.PlainText.StartsWith("Clear Stage 3 with Mistcutter\n")),
                "two distinct equipped weapons award two checks and fallback replay stays deduplicated"
            );
            for (var stage = 4; stage <= StageChecks.Count; stage++)
                client.ClearStage(stage, new[] { duelist });
            await Until(() => client.ConfirmedChecks == 22, client);
            Assert(
                notices.Count == 22 && notices.All(notice => notice.HasLocation),
                "weapon-specific stage rewards are announced exactly once"
            );
            Assert(!logs.Any(message => message.Contains("Goal sent")), "stage checks do not send a temporary goal");

            client.FirstKill(910301);
            await Until(() => client.ConfirmedChecks == 23, client);
            Assert(
                notices[^1].PlainText.StartsWith("First Kill: Lesser Husk\n"),
                "enemy reward announces its actual location"
            );
            client.FirstKill(910301);
            await PumpFor(client, 300);
            Assert(
                client.ConfirmedChecks == 23 && notices.Count == 23,
                "repeated enemy kills never duplicate a check or notice"
            );
            client.Disconnect();
            client.FirstKill(910302);
            client.FirstKill(910302);
            await Task.Delay(300);
            client = new ArchipelagoClient(directory, Log, notices.Add);
            client.Connect(server, "UYFTest", "");
            await Until(() => client.Connected && client.ConfirmedChecks == 24, client);
            Assert(
                notices.Count == 24 && notices[^1].PlainText.StartsWith("First Kill: Unraveled Husk\n"),
                "offline first kill survives client recreation and is announced only once"
            );
            foreach (var enemy in EnemyChecks.All.Skip(2))
                client.FirstKill(enemy.Id);
            await Until(() => client.ConfirmedChecks == 32, client);
            Assert(
                notices.Count == 32 && notices.All(notice => notice.HasLocation),
                "stage and enemy rewards retain location notices"
            );
            client.Disconnect();
            await Task.Delay(300);
            client = new ArchipelagoClient(directory, Log, notices.Add);
            client.Connect(server, "UYFTest", "");
            await Until(() => client.Connected && client.ConfirmedChecks == 32, client);
            Assert(
                client.StartingWeaponIds.SequenceEqual(new[] { 910024L }),
                "reconnect restores the same slot-selected starter"
            );
            await PumpFor(client, 300);
            client.FirstKill(910301);
            client.FirstKill(910310);
            await PumpFor(client, 300);
            Assert(
                notices.Count == 32 && client.ConfirmedChecks == 32,
                "replayed history restores ownership without duplicate notices/checks"
            );
            Console.WriteLine(
                "PASS: real AP server; weapon-specific stage checks, two-weapon clear, enemy checks, replay, offline checks and duplicate suppression"
            );
        }
        finally
        {
            client.Disconnect();
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

        void Heal(int amount)
        {
            Assert(amount == 5, "Shard of Vitality delivers 5 HP");
            effects++;
        }
    }

    private static async Task Until(Func<bool> condition, ArchipelagoClient client)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            client.Update();
            if (condition())
                return;
            await Task.Delay(25);
        }
        throw new Exception("Timed out waiting for AP server state.");
    }

    private static async Task PumpFor(ArchipelagoClient client, int milliseconds)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < deadline)
        {
            client.Update();
            await Task.Delay(25);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
