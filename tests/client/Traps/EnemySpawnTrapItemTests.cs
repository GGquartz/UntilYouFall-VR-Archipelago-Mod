using System.Reflection;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Helpers;
using UntilYouFallArchipelago;

internal static class EnemySpawnTrapItemTests
{
    public static void Run()
    {
        Assert(ApItems.EnemySpawnTrapId == 910450, "trap ID matches APWorld");
        var directory = Path.Combine(Path.GetTempPath(), "uyf-trap-item-test-" + Guid.NewGuid());
        const string identity = "server/seed-A/team0/slot1";
        var logs = new List<string>();
        var state = ApState.Open(directory, identity);
        var client = ConnectedClient(directory, state, logs);
        var pending = (SortedDictionary<int, long>)Field("_pendingItems").GetValue(client)!;
        pending[0] = ApItems.EnemySpawnTrapId;
        var effects = 0;
        void Spawn()
        {
            Assert(
                ApState.Open(directory, identity).HandledCount == client.HandledCount,
                "cursor is saved before the irreversible spawn callback"
            );
            effects++;
        }
        try
        {
            client.ApplyNextItem(
                new ItemEffects
                {
                    CanHeal = () => false,
                    Heal = _ => throw new Exception("unexpected heal"),
                    ApplyAetherFiller = (_, _) => throw new Exception("unexpected Aether"),
                    CanSpawnEnemy = () => false,
                    SpawnEnemy = Spawn,
                }
            );
            Assert(
                client.HandledCount == 0 && client.PendingItems == 1 && effects == 0,
                "loading defers the trap without consuming it"
            );
            client.ApplyNextItem(
                new ItemEffects
                {
                    CanHeal = () => false,
                    Heal = _ => { },
                    ApplyAetherFiller = (_, _) => false,
                    CanSpawnEnemy = () => true,
                    SpawnEnemy = Spawn,
                }
            );
            Assert(
                client.HandledCount == 1 && client.PendingItems == 0 && effects == 1,
                "trap dispatch is independent of healing or currency readiness"
            );
            client.ApplyNextItem(
                new ItemEffects
                {
                    CanHeal = () => false,
                    Heal = _ => { },
                    ApplyAetherFiller = (_, _) => false,
                    CanSpawnEnemy = () => true,
                    SpawnEnemy = Spawn,
                }
            );
            Assert(effects == 1, "consumed trap is not applied twice");

            pending[1] = ApItems.EnemySpawnTrapId;
            client.ApplyNextItem(
                new ItemEffects
                {
                    CanHeal = () => false,
                    Heal = _ => { },
                    ApplyAetherFiller = (_, _) => false,
                    CanSpawnEnemy = () => true,
                    SpawnEnemy = () => { },
                }
            );
            Assert(
                ApState.Open(directory, identity).HandledCount == 2 && client.PendingItems == 0,
                "spawn rejection or burst drop still permanently consumes the item"
            );

            var reloaded = ConnectedClient(directory, ApState.Open(directory, identity), logs);
            var replayed = (SortedDictionary<int, long>)Field("_pendingItems").GetValue(reloaded)!;
            replayed[0] = ApItems.EnemySpawnTrapId;
            replayed[1] = ApItems.EnemySpawnTrapId;
            reloaded.ApplyNextItem(
                new ItemEffects
                {
                    CanHeal = () => false,
                    Heal = _ => { },
                    ApplyAetherFiller = (_, _) => false,
                    CanSpawnEnemy = () => true,
                    SpawnEnemy = Spawn,
                }
            );
            Assert(
                reloaded.HandledCount == 2 && reloaded.PendingItems == 0 && effects == 1,
                "restart cannot replay handled trap indices"
            );
            reloaded.Disconnect();

            var burstSpawns = 0;
            long? previousRequest = null;
            var requestTime = 10000L;
            for (var trapIndex = 2; trapIndex < 7; trapIndex++)
            {
                pending[trapIndex] = ApItems.EnemySpawnTrapId;
                client.ApplyNextItem(
                    new ItemEffects
                    {
                        CanHeal = () => false,
                        Heal = _ => { },
                        ApplyAetherFiller = (_, _) => false,
                        CanSpawnEnemy = () => true,
                        SpawnEnemy = () =>
                        {
                            if (!EnemySpawnTrapRules.IsBurstDuplicate(requestTime, previousRequest))
                                burstSpawns++;
                            previousRequest = requestTime;
                            requestTime += 100;
                        },
                    }
                );
            }
            Assert(
                burstSpawns == 1 && client.HandledCount == 7 && client.PendingItems == 0,
                "five burst requests spawn once and consume all five item indices"
            );

            pending[7] = ApItems.EnemySpawnTrapId;
            client.ApplyNextItem(
                new ItemEffects
                {
                    CanHeal = () => false,
                    Heal = _ => { },
                    ApplyAetherFiller = (_, _) => false,
                    CanSpawnEnemy = () => true,
                    SpawnEnemy = () => throw new InvalidOperationException("test spawn error"),
                }
            );
            Assert(
                !client.Connected && ApState.Open(directory, identity).HandledCount == 8,
                "callback exception disconnects without rolling back a committed trap"
            );

            var failingDirectory = Path.Combine(directory, "failed-save");
            var failingState = ApState.Open(failingDirectory, identity);
            var failingClient = ConnectedClient(failingDirectory, failingState, logs);
            var failingPending = (SortedDictionary<int, long>)Field("_pendingItems").GetValue(failingClient)!;
            failingPending[0] = ApItems.EnemySpawnTrapId;
            Directory.Delete(failingDirectory, true);
            failingClient.ApplyNextItem(
                new ItemEffects
                {
                    CanHeal = () => false,
                    Heal = _ => { },
                    ApplyAetherFiller = (_, _) => false,
                    CanSpawnEnemy = () => true,
                    SpawnEnemy = Spawn,
                }
            );
            Assert(
                failingState.HandledCount == 0 && effects == 1 && !failingClient.Connected,
                "failed cursor save prevents spawning and disconnects"
            );
            Assert(logs.Any(message => message.Contains("Enemy Spawn Trap")), "AP dispatch keeps diagnostic logs");
        }
        finally
        {
            client.Disconnect();
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        Console.WriteLine("PASS: Enemy Spawn Trap dispatch, loading deferral, durable consume and failure safety");
        RunTimedTrap(ApItems.DodgeOnlyTrapId, "Dodge Only Trap");
        RunTimedTrap(ApItems.ExhaustionTrapId, "Exhaustion Trap");
        RunTimedTrap(ApItems.CursedTrapId, "Cursed Trap");
    }

    private static void RunTimedTrap(long itemId, string itemName)
    {
        Assert(
            itemId
                == (
                    itemName == "Dodge Only Trap" ? 910452
                    : itemName == "Exhaustion Trap" ? 910453
                    : 910454
                ),
            "timed trap ID matches APWorld"
        );
        var directory = Path.Combine(Path.GetTempPath(), "uyf-dodge-item-test-" + Guid.NewGuid());
        const string identity = "server/seed-A/team0/slot1";
        var logs = new List<string>();
        var state = ApState.Open(directory, identity);
        var client = ConnectedClient(directory, state, logs);
        var pending = (SortedDictionary<int, long>)Field("_pendingItems").GetValue(client)!;
        var trap = new NoBlockingTrapState();
        trap.LoadRoom(true);
        var effects = 0;
        var receipts = 0;
        void Receive()
        {
            Assert(
                ApState.Open(directory, identity).HandledCount == client.HandledCount,
                "Dodge Only cursor is durable before activation"
            );
            receipts++;
            if (trap.TryActivate(true, Array.Empty<(IntPtr, float)>()))
                effects++;
        }
        void Apply(bool ready, Action receive) =>
            client.ApplyNextItem(
                new ItemEffects
                {
                    CanHeal = () => false,
                    Heal = _ => throw new Exception("unexpected heal"),
                    ApplyAetherFiller = (_, _) => throw new Exception("unexpected Aether"),
                    CanSpawnEnemy = () => ready,
                    SpawnEnemy = () => throw new Exception("unexpected enemy spawn"),
                    DodgeOnlyTrap = itemId == ApItems.DodgeOnlyTrapId ? receive : null,
                    ExhaustionTrap = itemId == ApItems.ExhaustionTrapId ? receive : null,
                    CursedTrap = itemId == ApItems.CursedTrapId ? receive : null,
                }
            );
        try
        {
            pending[0] = itemId;
            Apply(false, Receive);
            Assert(
                client.HandledCount == 0 && client.PendingItems == 1 && effects == 0,
                "Dodge Only waits during loading without consuming the item"
            );
            Apply(true, Receive);
            Assert(
                client.HandledCount == 1 && client.PendingItems == 0 && effects == 1 && trap.Active,
                "Dodge Only dispatches the existing activation callback"
            );
            trap.Advance(20, false);
            pending[1] = itemId;
            Apply(true, Receive);
            Assert(
                client.HandledCount == 2 && effects == 2 && trap.Active && trap.RemainingSeconds == 60,
                "duplicate receipt durably resets the timer without stacking"
            );
            pending[2] = itemId;
            client.ApplyNextItem(
                new ItemEffects
                {
                    CanHeal = () => false,
                    Heal = _ => { },
                    ApplyAetherFiller = (_, _) => false,
                    CanSpawnEnemy = () => true,
                    SpawnEnemy = () => { },
                }
            );
            Assert(
                client.HandledCount == 2 && client.PendingItems == 1,
                "missing Dodge Only handler cannot silently consume the receipt"
            );
            Apply(true, () => { });
            Assert(
                client.HandledCount == 3 && client.PendingItems == 0,
                "out-of-encounter rejection still consumes the receipt"
            );

            var reloaded = ConnectedClient(directory, ApState.Open(directory, identity), logs);
            var replayed = (SortedDictionary<int, long>)Field("_pendingItems").GetValue(reloaded)!;
            replayed[0] = itemId;
            reloaded.ApplyNextItem(
                new ItemEffects
                {
                    CanHeal = () => false,
                    Heal = _ => { },
                    ApplyAetherFiller = (_, _) => false,
                    CanSpawnEnemy = () => true,
                    SpawnEnemy = () => { },
                    DodgeOnlyTrap = itemId == ApItems.DodgeOnlyTrapId ? Receive : null,
                    ExhaustionTrap = itemId == ApItems.ExhaustionTrapId ? Receive : null,
                    CursedTrap = itemId == ApItems.CursedTrapId ? Receive : null,
                }
            );
            Assert(
                reloaded.HandledCount == 3 && effects == 2 && receipts == 2,
                "Dodge Only cannot replay handled indices after restart"
            );
            reloaded.Disconnect();

            pending[3] = itemId;
            Apply(true, () => throw new InvalidOperationException("test dodge error"));
            Assert(
                !client.Connected && ApState.Open(directory, identity).HandledCount == 4,
                "Dodge Only callback failure preserves the committed cursor"
            );

            var failingDirectory = Path.Combine(directory, "failed-save");
            var failingState = ApState.Open(failingDirectory, identity);
            var failingClient = ConnectedClient(failingDirectory, failingState, logs);
            var failingPending = (SortedDictionary<int, long>)Field("_pendingItems").GetValue(failingClient)!;
            failingPending[0] = itemId;
            Directory.Delete(failingDirectory, true);
            failingClient.ApplyNextItem(
                new ItemEffects
                {
                    CanHeal = () => false,
                    Heal = _ => { },
                    ApplyAetherFiller = (_, _) => false,
                    CanSpawnEnemy = () => true,
                    SpawnEnemy = () => { },
                    DodgeOnlyTrap = itemId == ApItems.DodgeOnlyTrapId ? Receive : null,
                    ExhaustionTrap = itemId == ApItems.ExhaustionTrapId ? Receive : null,
                    CursedTrap = itemId == ApItems.CursedTrapId ? Receive : null,
                }
            );
            Assert(
                failingState.HandledCount == 0 && effects == 2 && !failingClient.Connected,
                "Dodge Only save failure prevents activation"
            );
            Assert(logs.Any(message => message.Contains("item=" + itemName)), "Dodge Only receipt is logged");
        }
        finally
        {
            client.Disconnect();
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        Console.WriteLine($"PASS: {itemName} AP dispatch, durable consumption, nonstack and failure safety");
    }

    private static ArchipelagoClient ConnectedClient(string directory, ApState state, List<string> logs)
    {
        var session = ArchipelagoSessionFactory.CreateSession("ws://localhost:1");
        typeof(ArchipelagoSession)
            .GetField("<Socket>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(session, DispatchProxy.Create<IArchipelagoSocketHelper, EnemyTrapSocketProxy>());
        var client = new ArchipelagoClient(directory, logs.Add);
        ClientSessionTestHelpers.AttachConnection(client, session);
        ClientSessionTestHelpers.AttachState(client, state);
        ClientSessionTestHelpers.SetLoggedIn(client, true);
        return client;
    }

    private static FieldInfo Field(string name) =>
        typeof(ArchipelagoClient).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static void Assert(bool ok, string message)
    {
        if (!ok)
            throw new Exception("Enemy Spawn Trap items: " + message);
    }
}

public class EnemyTrapSocketProxy : DispatchProxy
{
    public int Disconnects { get; private set; }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == "DisconnectAsync")
        {
            Disconnects++;
            return Task.CompletedTask;
        }
        return targetMethod?.Name switch
        {
            "get_Connected" => true,
            _ => throw new InvalidOperationException("Unexpected test socket call: " + targetMethod?.Name),
        };
    }
}
