using System.Reflection;
using System.Text.Json;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Helpers;
using UntilYouFallArchipelago;

internal static class RunOnlyEffectTests
{
    public static void Run()
    {
        foreach (var item in new[] { ApItems.SuperChargeId, ApItems.EnemyStasisId })
        {
            CheckDispatch(item);
            CheckVitalityBeforeRunOnlyEffect(item);
        }
        Console.WriteLine(
            "PASS: run-only effect receipt gate, duplicate history, loading, durable consume, save compatibility and failures"
        );
    }

    private static void CheckVitalityBeforeRunOnlyEffect(long item)
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-vitality-run-only-effect-" + Guid.NewGuid());
        const string identity = "server/seed/team0/slot1";
        var state = ApState.Open(directory, identity);
        var client = new ArchipelagoClient(directory, _ => { }, canReceiveRunOnlyEffects: () => true);
        var session = ArchipelagoSessionFactory.CreateSession("ws://localhost:1");
        typeof(ArchipelagoSession)
            .GetField("<Socket>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(session, DispatchProxy.Create<IArchipelagoSocketHelper, EnemyTrapSocketProxy>());
        ClientSessionTestHelpers.AttachConnection(client, session);
        ClientSessionTestHelpers.AttachState(client, state);
        ClientSessionTestHelpers.SetLoggedIn(client, true);
        var pending = (SortedDictionary<int, long>)Field("_pendingItems").GetValue(client)!;
        pending[0] = ApItems.ShardOfVitalityId;
        pending[1] = item;
        client.RecordRunOnlyEffectReceipt(1, item);
        var health = 3;
        var heals = 0;
        var effects = 0;
        void Heal(int amount)
        {
            Check(
                amount == 5 && ApState.Open(directory, identity).HandledCount == 1,
                "bounded five HP restore is committed before effect"
            );
            health = Math.Min(3, health + amount);
            heals++;
        }
        void Consume(bool ready) =>
            client.ApplyNextItem(
                new ItemEffects
                {
                    CanHeal = () => ready,
                    Heal = Heal,
                    ApplyAetherFiller = (_, _) => false,
                    CanSpawnEnemy = () => ready,
                    SpawnEnemy = () => { },
                    SuperCharge = () => effects++,
                    EnemyStasis = () => effects++,
                }
            );
        try
        {
            Consume(false);
            Check(client.HandledCount == 0 && heals == 0, "unavailable player keeps Shard pending");
            Consume(true);
            Consume(true);
            Consume(true);
            Check(
                health == 3 && heals == 1 && effects == 1 && client.HandledCount == 2 && pending.Count == 0,
                "full HP consumes Shard once and allows the following run-only effect"
            );
            ClientSessionTestHelpers.AttachState(client, ApState.Open(directory, identity));
            pending[0] = ApItems.ShardOfVitalityId;
            pending[1] = item;
            Consume(true);
            Check(heals == 1 && effects == 1, "saved cursor prevents consumed history from replaying");
        }
        finally
        {
            client.Disconnect();
            Directory.Delete(directory, true);
        }
    }

    private static void CheckDispatch(long item)
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-run-only-effect-" + Guid.NewGuid());
        const string identity = "server/seed/team0/slot1";
        var state = ApState.Open(directory, identity);
        var inRun = false;
        var client = new ArchipelagoClient(directory, _ => { }, canReceiveRunOnlyEffects: () => inRun);
        var session = ArchipelagoSessionFactory.CreateSession("ws://localhost:1");
        typeof(ArchipelagoSession)
            .GetField("<Socket>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(session, DispatchProxy.Create<IArchipelagoSocketHelper, EnemyTrapSocketProxy>());
        ClientSessionTestHelpers.AttachConnection(client, session);
        ClientSessionTestHelpers.AttachState(client, state);
        ClientSessionTestHelpers.SetLoggedIn(client, true);
        var pending = (SortedDictionary<int, long>)Field("_pendingItems").GetValue(client)!;
        var effects = 0;
        void Apply()
        {
            Check(ApState.Open(directory, identity).HandledCount == client.HandledCount, "saved before effect");
            effects++;
        }
        void Consume(bool loading = false, bool chargeReady = true, Action? effect = null) =>
            client.ApplyNextItem(
                new ItemEffects
                {
                    CanHeal = () => false,
                    Heal = _ => throw new Exception("heal"),
                    ApplyAetherFiller = (_, _) => false,
                    CanSpawnEnemy = () => !loading,
                    SpawnEnemy = () => throw new Exception("spawn"),
                    SuperCharge = effect ?? Apply,
                    EnemyStasis = effect ?? Apply,
                    CanApplySuperCharge = () => chargeReady,
                }
            );
        try
        {
            pending[0] = item;
            client.RecordRunOnlyEffectReceipt(0, item);
            inRun = true;
            ((Dictionary<int, bool>)Field("_runOnlyEffectReceipts").GetValue(client)!).Clear();
            ClientSessionTestHelpers.AttachState(client, ApState.Open(directory, identity));
            client.RecordRunOnlyEffectReceipt(0, item);
            Consume();
            Check(effects == 0 && client.HandledCount == 1, "hub receipt stays void behind a cursor or duplicate");
            Check(ApState.Open(directory, identity).VoidedRunOnlyEffects.Count == 0, "committed void index is pruned");
            pending[1] = item;
            client.RecordRunOnlyEffectReceipt(1, item);
            Consume(loading: true);
            Check(client.HandledCount == 1 && effects == 0, "loading defers eligible item");
            if (item == ApItems.SuperChargeId)
            {
                Consume(chargeReady: false);
                Check(client.HandledCount == 1, "wait for active super without partial refill");
            }
            Consume();
            Consume();
            Check(effects == 1 && client.HandledCount == 2, "applies once and consumes");
            pending[2] = item;
            client.RecordRunOnlyEffectReceipt(2, item);
            client.VoidRunOnlyEffects();
            Consume();
            Check(effects == 1 && client.HandledCount == 3, "delayed receipt cannot carry into another run");
            pending[3] = item;
            client.RecordRunOnlyEffectReceipt(3, item);
            Consume(effect: () => throw new Exception("expected callback failure"));
            Check(
                !client.Connected && ApState.Open(directory, identity).HandledCount == 4,
                "callback failure cannot replay committed item"
            );
            var saved = ApState.Open(directory, identity);
            saved.VoidRunOnlyEffect(8);
            using (
                var document = JsonDocument.Parse(File.ReadAllText(Directory.GetFiles(directory, "*.json").Single()))
            )
            {
                Check(
                    document
                        .RootElement.GetProperty("VoidedRunFillers")
                        .EnumerateArray()
                        .Any(index => index.GetInt32() == 8),
                    "run-only effect rename preserves the existing save field"
                );
                Check(
                    !document.RootElement.TryGetProperty(nameof(ApState.VoidedRunOnlyEffects), out _),
                    "runtime property rename does not change the save format"
                );
            }
            Check(ApState.Open(directory, identity).VoidedRunOnlyEffects.Contains(8), "void survives state reload");
            saved.ResetItemHistory();
            Check(
                ApState.Open(directory, identity).VoidedRunOnlyEffects.Count == 0,
                "server history reset clears old voids"
            );
        }
        finally
        {
            client.Disconnect();
            Directory.Delete(directory, true);
        }
    }

    private static FieldInfo Field(string name) =>
        typeof(ArchipelagoClient).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static void Check(bool value, string message)
    {
        if (!value)
            throw new Exception("Run-only effect: " + message);
    }
}
