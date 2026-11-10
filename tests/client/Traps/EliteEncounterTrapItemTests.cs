using System.Reflection;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Helpers;
using UntilYouFallArchipelago;

internal static class EliteEncounterTrapItemTests
{
    public static void Run()
    {
        Assert(ApItems.EliteEncounterTrapId == 910451, "stable AP ID");
        var directory = Path.Combine(Path.GetTempPath(), "uyf-elite-items-" + Guid.NewGuid());
        const string identity = "server/seed/team0/slot1";
        var state = ApState.Open(directory, identity);
        var client = ConnectedClient(directory, state);
        var pending = (SortedDictionary<int, long>)Field("_pendingItems").GetValue(client)!;
        try
        {
            pending[0] = ApItems.ShardOfVitalityId;
            for (var index = 1; index <= 5; index++)
                pending[index] = ApItems.EliteEncounterTrapId;
            client.Update();
            var token = client.PendingEliteEncounterTrap;
            Assert(
                token != null && state.LastEliteEncounterTrapIndex == 5 && client.HandledCount == 0,
                "the whole burst arms once even behind an unconsumable heal"
            );
            Assert(
                ApState.Open(directory, identity).PendingEliteEncounterTrap == token,
                "arming is durable before gameplay or the consumable cursor"
            );
            Assert(
                ApState.Open(directory, "another-slot").PendingEliteEncounterTrap == null,
                "pending effects cannot cross AP identities"
            );
            client.CompleteEliteEncounterTrap(token!);
            client.Update();
            Assert(client.PendingEliteEncounterTrap == null, "unconsumed history cannot rearm the already used burst");
            client.ApplyNextItem(
                new ItemEffects
                {
                    CanHeal = () => true,
                    Heal = _ => { },
                    ApplyAetherFiller = (_, _) => false,
                    CanSpawnEnemy = () => false,
                    SpawnEnemy = () => { },
                }
            );
            for (var index = 1; index <= 5; index++)
                client.ApplyNextItem(
                    new ItemEffects
                    {
                        CanHeal = () => false,
                        Heal = _ => { },
                        ApplyAetherFiller = (_, _) => false,
                        CanSpawnEnemy = () => false,
                        SpawnEnemy = () => { },
                    }
                );
            Assert(
                client.HandledCount == 6 && client.PendingEliteEncounterTrap == null,
                "cursor catches up during loading without replaying old elite receipts"
            );

            pending[6] = ApItems.EliteEncounterTrapId;
            client.Update();
            var nextToken = client.PendingEliteEncounterTrap;
            Assert(nextToken != null && nextToken != token, "only a fresh item arms a new room");
            client.CompleteEliteEncounterTrap(token!);
            Assert(
                client.PendingEliteEncounterTrap == nextToken,
                "stale load acknowledgements cannot consume a new item"
            );
            client.Disconnect();
            Assert(client.PendingEliteEncounterTrap == null, "disconnected clients cannot spend pending effects");
            Assert(
                ApState.Open(directory, identity).PendingEliteEncounterTrap == nextToken,
                "disconnect and restart retain the pending item"
            );

            var restored = ConnectedClient(directory, ApState.Open(directory, identity));
            try
            {
                var replay = (SortedDictionary<int, long>)Field("_pendingItems").GetValue(restored)!;
                for (var index = 1; index <= 6; index++)
                    replay[index] = ApItems.EliteEncounterTrapId;
                restored.Update();
                Assert(restored.PendingEliteEncounterTrap == nextToken, "reconnect does not replace the pending token");
                restored.CompleteEliteEncounterTrap(nextToken!);
                restored.Update();
                Assert(restored.PendingEliteEncounterTrap == null, "replayed receipt indices stay consumed");
            }
            finally
            {
                restored.Disconnect();
            }

            state.ResetItemHistory();
            Assert(
                state.LastEliteEncounterTrapIndex == -1 && state.PendingEliteEncounterTrap == null,
                "server history reset clears the receipt watermark and pending item"
            );
            state.RegisterEliteEncounterTrap(0);
            Assert(
                state.PendingEliteEncounterTrap != null && state.PendingEliteEncounterTrap != nextToken,
                "reset history creates a new unique token even when indices are reused"
            );

            var deathToken = state.PendingEliteEncounterTrap!;
            Assert(
                !state.RegisterDeathLinkEliteTrap() && state.PendingEliteEncounterTrap == deathToken,
                "death link elite trap coalesces with a pending trap"
            );
            state.CompleteEliteEncounterTrap(deathToken);
            Assert(
                state.RegisterDeathLinkEliteTrap()
                    && state.PendingEliteEncounterTrap != null
                    && ApState.Open(directory, identity).PendingEliteEncounterTrap == state.PendingEliteEncounterTrap,
                "death link elite trap is durable without an item index"
            );

            var failureDirectory = Path.Combine(directory, "failure");
            var failureState = ApState.Open(failureDirectory, identity);
            Directory.Delete(failureDirectory, true);
            AssertThrows(() => failureState.RegisterEliteEncounterTrap(0), "arming save failure is visible");
            Assert(
                failureState.LastEliteEncounterTrapIndex == -1 && failureState.PendingEliteEncounterTrap == null,
                "failed arming rolls back the watermark and pending state"
            );
            Directory.CreateDirectory(failureDirectory);
            failureState.RegisterEliteEncounterTrap(0);
            var failureToken = failureState.PendingEliteEncounterTrap!;
            Directory.Delete(failureDirectory, true);
            AssertThrows(
                () => failureState.CompleteEliteEncounterTrap(failureToken),
                "completion save failure is visible"
            );
            Assert(
                failureState.PendingEliteEncounterTrap == failureToken,
                "failed completion keeps the pending effect"
            );
            var failingClient = ConnectedClient(failureDirectory, failureState);
            try
            {
                var failedPending = (SortedDictionary<int, long>)Field("_pendingItems").GetValue(failingClient)!;
                failedPending[0] = ApItems.EliteEncounterTrapId;
                failingClient.ApplyNextItem(
                    new ItemEffects
                    {
                        CanHeal = () => false,
                        Heal = _ => { },
                        ApplyAetherFiller = (_, _) => false,
                        CanSpawnEnemy = () => false,
                        SpawnEnemy = () => { },
                    }
                );
                Assert(
                    !failingClient.Connected
                        && failureState.HandledCount == 0
                        && failureState.PendingEliteEncounterTrap == failureToken,
                    "cursor save failure disconnects without dropping the independently persisted pending receipt"
                );
            }
            finally
            {
                failingClient.Disconnect();
            }
        }
        finally
        {
            client.Disconnect();
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        Console.WriteLine(
            "PASS: Elite Encounter Trap burst receipts, cursor independence, reconnect and persistence rollback"
        );
    }

    private static ArchipelagoClient ConnectedClient(string directory, ApState state)
    {
        var session = ArchipelagoSessionFactory.CreateSession("ws://localhost:1");
        typeof(ArchipelagoSession)
            .GetField("<Socket>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(session, DispatchProxy.Create<IArchipelagoSocketHelper, EnemyTrapSocketProxy>());
        var client = new ArchipelagoClient(directory, _ => { });
        ClientSessionTestHelpers.AttachConnection(client, session);
        ClientSessionTestHelpers.AttachState(client, state);
        ClientSessionTestHelpers.SetLoggedIn(client, true);
        return client;
    }

    private static FieldInfo Field(string name) =>
        typeof(ArchipelagoClient).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception("Elite Encounter Trap items: " + message);
    }

    private static void AssertThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (IOException)
        {
            return;
        }
        throw new Exception("Elite Encounter Trap items: " + message);
    }
}
