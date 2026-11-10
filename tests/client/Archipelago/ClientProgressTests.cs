using System.Reflection;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Helpers;
using UntilYouFallArchipelago;

internal static class ClientProgressTests
{
    public static void Run()
    {
        OfflineChecksAndSendFailures();
        GoalUsesQuestProgress();
        Console.WriteLine("PASS: client progress ownership, offline resend, stale send failures and quest-based goals");
    }

    private static void OfflineChecksAndSendFailures()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-client-progress-" + Guid.NewGuid());
        var state = ApState.Open(directory, "progress-checks");
        var logs = new List<string>();
        var client = new ArchipelagoClient(directory, logs.Add);
        var session = ClientSessionTestHelpers.Attach(client, state);
        var locations = DispatchProxy.Create<ILocationCheckHelper, ClientProgressLocationProxy>();
        var proxy = (ClientProgressLocationProxy)(object)locations;
        typeof(ArchipelagoSession)
            .GetField("<Locations>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(session, locations);
        var progress = ClientSessionTestHelpers.Progress(client);
        try
        {
            ClientSessionTestHelpers.SetLoggedIn(client, false);
            client.FirstKill(EnemyChecks.All[0].Id);
            Check(
                ApState.Open(directory, state.Identity).Checks.Contains(EnemyChecks.All[0].Id),
                "offline check saves"
            );
            Check(proxy.Sends == 0, "offline checks do not send");
            ClientSessionTestHelpers.SetLoggedIn(client, true);
            progress.SendChecks(state.Checks);
            Check(SpinWait.SpinUntil(() => proxy.Sends == 1, 5000), "saved offline checks resend");
            progress.HandleCheckedLocations([EnemyChecks.All[0].Id], false);
            progress.SendChecks(state.Checks);
            Check(proxy.Sends == 1 && client.ConfirmedChecks == 1, "confirmed checks do not resend");

            var failedSend = new TaskCompletionSource();
            proxy.Completion = failedSend.Task;
            client.FirstKill(EnemyChecks.All[1].Id);
            Check(SpinWait.SpinUntil(() => proxy.Sends == 2, 5000), "unconfirmed check sends");
            failedSend.SetException(new IOException("current-send-failure"));
            WaitForLog(client, logs, "Check send failed - saved for reconnect: current-send-failure");
            Check(
                state.Checks.Contains(EnemyChecks.All[1].Id) && client.Connected,
                "send failure preserves check and connection"
            );

            var staleSend = new TaskCompletionSource();
            proxy.Completion = staleSend.Task;
            client.FirstKill(EnemyChecks.All[2].Id);
            Check(SpinWait.SpinUntil(() => proxy.Sends == 3, 5000), "stale send started");
            client.Disconnect();
            ClientSessionTestHelpers.AttachConnection(client, session);
            staleSend.SetException(new IOException("stale-send-failure"));
            var events = (System.Collections.Concurrent.ConcurrentQueue<Action>)
                ClientSessionTestHelpers.GetField(ClientSessionTestHelpers.Connection(client), "_events");
            Check(SpinWait.SpinUntil(() => !events.IsEmpty, 5000), "stale send callback was queued");
            client.PostSessionEvent(
                ClientSessionTestHelpers.Connection(client).Generation - 1,
                () => throw new Exception("stale callback")
            );
            client.Update();
            Check(
                client.Connected && !logs.Any(log => log.Contains("stale-send-failure")),
                "stale send cannot affect replacement"
            );
        }
        finally
        {
            client.Disconnect();
            Directory.Delete(directory, true);
        }
    }

    private static void GoalUsesQuestProgress()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-client-goal-" + Guid.NewGuid());
        var state = ApState.Open(directory, "progress-goal");
        var client = new ArchipelagoClient(directory, _ => { });
        var session = ClientSessionTestHelpers.Attach(client, state);
        var socket = DispatchProxy.Create<IArchipelagoSocketHelper, ClientProgressSocketProxy>();
        var proxy = (ClientProgressSocketProxy)(object)socket;
        typeof(ArchipelagoSession)
            .GetField("<Socket>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(session, socket);
        ClientSessionTestHelpers.SetSettings(client, "Goal", new GoalSettings(GoalMode.Quests, 1));
        ClientSessionTestHelpers.SetSettings(client, "ActiveQuests", new[] { 0 });
        try
        {
            client.TrySendGoal();
            Check(proxy.Goals == 0, "unfinished quest blocks goal");
            state.SetQuestProgress(0, QuestChecks.All[0].Target);
            client.TrySendGoal();
            client.TrySendGoal();
            Check(
                proxy.Goals == 1 && state.Checks.Count == 0,
                "quest progress completes goal without reward checks, only once"
            );
            ClientSessionTestHelpers.Progress(client).ResetGoalSent();
            proxy.Fail = true;
            try
            {
                client.TrySendGoal();
                throw new Exception("goal send failure swallowed");
            }
            catch (IOException) { }
            proxy.Fail = false;
            client.TrySendGoal();
            Check(proxy.Goals == 2, "goal flag is set before SDK send, including failure");
        }
        finally
        {
            client.Disconnect();
            Directory.Delete(directory, true);
        }
    }

    private static void WaitForLog(ArchipelagoClient client, List<string> logs, string message)
    {
        Check(
            SpinWait.SpinUntil(
                () =>
                {
                    client.Update();
                    return logs.Any(log => log.Contains(message));
                },
                5000
            ),
            message
        );
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception("Client progress: " + message);
    }
}

public class ClientProgressLocationProxy : DispatchProxy
{
    private int _sends;
    public int Sends => Volatile.Read(ref _sends);
    public Task Completion { get; set; } = Task.CompletedTask;

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.Name != "CompleteLocationChecksAsync")
            throw new InvalidOperationException("Unexpected progress location call: " + method?.Name);
        var completion = Completion;
        Interlocked.Increment(ref _sends);
        return completion;
    }
}

public class ClientProgressSocketProxy : DispatchProxy
{
    public int Goals { get; private set; }
    public bool Fail { get; set; }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.Name == "get_Connected")
            return true;
        if (method?.Name == "DisconnectAsync")
            return Task.CompletedTask;
        if (method?.Name == "SendPacket")
        {
            Goals++;
            if (Fail)
                throw new IOException("goal-send-failure");
            return null;
        }
        throw new InvalidOperationException("Unexpected progress socket call: " + method?.Name);
    }
}
