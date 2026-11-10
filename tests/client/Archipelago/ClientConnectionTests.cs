using System.Reflection;
using System.Text.Json;
using Archipelago.MultiClient.Net;
using UntilYouFallArchipelago;

internal static class ClientConnectionTests
{
    public static void Run()
    {
        SessionEvents();
        PendingSessionData();
        SessionInitialization();
        CancelledLoginAndSocketClosure();
        InitializationFailureKeepsMatchingProgressState();
        Console.WriteLine("PASS: client main-thread dispatch, stale connections, login ordering and run eligibility");
    }

    private static void SessionEvents()
    {
        var client = new ArchipelagoClient(Path.GetTempPath(), _ => { });
        var applied = 0;
        client.PostSessionEvent(0, () => applied++);
        Check(applied == 0, "session callbacks wait for the main-thread update");
        client.Update();
        Check(applied == 1, "current session callbacks run during update");

        client.PostSessionEvent(0, () => throw new Exception("stale session must not execute"));
        client.Disconnect();
        client.PostSessionEvent(1, () => applied++);
        client.Update();
        Check(applied == 2, "a new connection ignores queued callbacks from the previous generation");

        client.PostSessionEvent(1, () => throw new InvalidDataException("invalid session data"));
        ((SortedDictionary<int, long>)ClientSessionTestHelpers.GetField(client, "_pendingItems"))[0] =
            ApItems.ShardOfVitalityId;
        ((Dictionary<long, StageRewardItemInfo>)ClientSessionTestHelpers.GetField(client, "_stageRewardInfo"))[123] =
            null!;
        ClientSessionTestHelpers.SetProperty(client, "WeaponHintsEnabled", true);
        client.PostSessionEvent(1, () => applied++);
        client.Update();
        Check(applied == 2, "a processing failure invalidates remaining events from that connection");
        Check(
            client.PendingItems == 0 && !client.WeaponHintsEnabled && !client.TryGetStageRewardInfo(123, out _),
            "processing failure performs full feature cleanup"
        );
    }

    private static void PendingSessionData()
    {
        var eligible = false;
        var client = new ArchipelagoClient(Path.GetTempPath(), _ => { }, canReceiveRunOnlyEffects: () => eligible);
        var pending = new Queue<Action>();
        var received = new List<bool>();
        client.PostSessionData(0, pending, received.Add);
        eligible = true;
        client.Update();
        Check(received.Count == 0 && pending.Count == 1, "pre-login data waits for validated session state");
        eligible = false;
        SetLoggedIn(client, true);
        pending.Dequeue()();
        Check(received.SequenceEqual(new[] { true }), "eligibility is captured when the main-thread event runs");

        SetLoggedIn(client, false);
        eligible = true;
        client.PostSessionData(0, pending, received.Add);
        client.Update();
        client.VoidRunOnlyEffects();
        SetLoggedIn(client, true);
        pending.Dequeue()();
        Check(received.SequenceEqual(new[] { true, false }), "ending the run invalidates staged effect eligibility");

        client.PostSessionData(0, pending, received.Add);
        client.Update();
        Check(pending.Count == 0 && received[^1], "validated session data is applied without a pre-login delay");

        client.PostSessionData(0, pending, _ => throw new Exception("cancelled data must not execute"));
        client.Disconnect();
        client.Update();
        Check(pending.Count == 0, "cancelled connection data cannot enter a later login queue");
    }

    private static void SessionInitialization()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-session-initialization-" + Guid.NewGuid());
        var identity = JsonSerializer.Serialize(
            new
            {
                seed = "original",
                Team = 0,
                Slot = 1,
            }
        );
        var state = ApState.Open(directory, identity);
        state.SetWeaponUpgradeItemCount(3);
        state.RecordCheck(ArchipelagoClient.BaseCheckIds[0]);
        var client = new ArchipelagoClient(directory, _ => { });
        var session = ClientSessionTestHelpers.Attach(client, state);
        var history = (ReceivedItemHistory)ClientSessionTestHelpers.GetField(client, "_receivedItemHistory");
        history.Record(0, ApItems.ProgressiveShopUnlockId);
        history.Record(1, ApItems.ProgressiveWeaponUpgradeLevelId);
        var slotData = ClientSlotSettingsTests.ValidSlotData();
        slotData["death_link"] = false;
        try
        {
            slotData["weapon_hints"] = "invalid";
            try
            {
                ClientSessionTestHelpers.Initialize(client, session, slotData, "invalid");
                throw new Exception("Invalid session settings were accepted");
            }
            catch (TargetInvocationException exception) when (exception.InnerException is FormatException)
            {
                Check(
                    ReferenceEquals(ClientSessionTestHelpers.GetField(client, "_state"), state),
                    "invalid settings retain the previous validated state"
                );
                Check(
                    client.WeaponUpgradeItemCount == 3 && client.ShopUnlockCount == 1,
                    "invalid settings leave receipt counts intact"
                );
                Check(Directory.GetFiles(directory).Length == 1, "invalid settings cannot create a new seed save");
            }
            slotData["weapon_hints"] = true;
            var order = new List<int>();
            var pending = new Queue<Action>();
            pending.Enqueue(() =>
            {
                Check(
                    client.StartingHands == StartingHands.LeftOnly && client.Connected,
                    "staged events see validated settings"
                );
                order.Add(1);
            });
            pending.Enqueue(() => order.Add(2));
            ClientSessionTestHelpers.Initialize(client, session, slotData, "original", pending);
            Check(order.SequenceEqual(new[] { 1, 2 }), "initialization drains pre-login data in arrival order");
            Check(
                client.WeaponUpgradeItemCount == 3 && client.ShopUnlockCount == 1,
                "same-slot login preserves durable counts and shop availability"
            );
            Check(
                history.Count(ApItems.ProgressiveWeaponUpgradeLevelId) == 0,
                "login rebuilds receipt counters without discarding saved ownership"
            );
            Check(
                ApState.Open(directory, identity).Checks.Contains(ArchipelagoClient.BaseCheckIds[0]),
                "initialization preserves offline checks"
            );

            ClientSessionTestHelpers.Initialize(client, session, slotData, "different");
            Check(
                client.ShopUnlockCount == 0 && client.WeaponUpgradeItemCount == 0,
                "a different slot identity cannot inherit old receipt counts"
            );
            Check(
                ApState.Open(directory, identity).WeaponUpgradeItemCount == 3,
                "switching identities cannot overwrite the previous save"
            );
        }
        finally
        {
            client.Disconnect();
            Directory.Delete(directory, true);
        }
    }

    private static void CancelledLoginAndSocketClosure()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-session-cancellation-" + Guid.NewGuid());
        var state = ApState.Open(directory, "validated-session");
        var client = new ArchipelagoClient(directory, _ => { });
        var session = ClientSessionTestHelpers.Attach(client, state);
        var lost = 0;
        client.ConnectionLost = () => lost++;
        try
        {
            var cancelledSession = ArchipelagoSessionFactory.CreateSession("ws://localhost:1");
            var cancelledSocket = DispatchProxy.Create<
                Archipelago.MultiClient.Net.Helpers.IArchipelagoSocketHelper,
                EnemyTrapSocketProxy
            >();
            typeof(ArchipelagoSession)
                .GetField("<Socket>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(cancelledSession, cancelledSocket);
            var attempt = new ClientConnection.Attempt(cancelledSession, -1, "localhost:1", "Player1");
            attempt.Cancellation.Cancel();
            ClientSessionTestHelpers
                .Connection(client)
                .Login(attempt, "", (_, _) => throw new Exception("cancelled login initialized"));
            client.Update();
            Check(
                client.Connected && lost == 0,
                "an already-cancelled login does not attempt a new login or replace state"
            );
            Check(
                ((EnemyTrapSocketProxy)cancelledSocket).Disconnects == 1,
                "cancelled login disconnects its captured socket"
            );
            Check(
                ((EnemyTrapSocketProxy)session.Socket).Disconnects == 0,
                "cancelled login leaves the replacement socket alone"
            );
            var close = typeof(ArchipelagoClient).GetMethod(
                "HandleSocketClosed",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!;
            client.PostSessionEvent(-1, () => close.Invoke(client, ["stale socket"]));
            client.Update();
            Check(client.Connected && lost == 0, "an old socket closure cannot disconnect a replacement session");
            client.PostSessionEvent(0, () => close.Invoke(client, ["current socket"]));
            client.Update();
            Check(!client.Connected && lost == 1, "current socket closure reports connection loss once");
            Check(
                ReferenceEquals(ClientSessionTestHelpers.GetField(client, "_state"), state),
                "socket closure preserves the validated save for offline checks"
            );
        }
        finally
        {
            client.Disconnect();
            Directory.Delete(directory, true);
        }
    }

    private static void InitializationFailureKeepsMatchingProgressState()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-initialization-failure-" + Guid.NewGuid());
        var previous = ApState.Open(directory, "previous-slot");
        var client = new ArchipelagoClient(directory, _ => { });
        var session = ClientSessionTestHelpers.Attach(client, previous);
        SetLoggedIn(client, false);
        try
        {
            try
            {
                ClientSessionTestHelpers.Initialize(
                    client,
                    session,
                    ClientSlotSettingsTests.ValidSlotData(),
                    "new-slot"
                );
                throw new Exception("test socket must reject DeathLink subscription");
            }
            catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException) { }
            var state = (ApState)ClientSessionTestHelpers.GetField(client, "_state");
            Check(
                state.Identity != previous.Identity,
                "validated settings select the new state before DeathLink setup"
            );
            client.Disconnect();
            client.FirstKill(EnemyChecks.All[0].Id);
            Check(
                state.Checks.Contains(EnemyChecks.All[0].Id) && previous.Checks.Count == 0,
                "initialization failure keeps progress and facade on the same state"
            );
        }
        finally
        {
            client.Disconnect();
            Directory.Delete(directory, true);
        }
    }

    private static void SetLoggedIn(ArchipelagoClient client, bool loggedIn) =>
        ClientSessionTestHelpers.SetLoggedIn(client, loggedIn);

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception("Client connection: " + message);
    }
}
