using System.Reflection;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using UntilYouFallArchipelago;

internal static class ClientSessionTestHelpers
{
    public static ArchipelagoSession Attach(ArchipelagoClient client, ApState state)
    {
        var session = ArchipelagoSessionFactory.CreateSession("ws://localhost:1");
        SetSessionHelper(session, "Socket", DispatchProxy.Create<IArchipelagoSocketHelper, EnemyTrapSocketProxy>());
        SetSessionHelper(session, "Items", DispatchProxy.Create<IReceivedItemsHelper, ClientSessionTestProxy>());
        SetSessionHelper(session, "Players", DispatchProxy.Create<IPlayerHelper, ClientSessionTestProxy>());
        SetSessionHelper(session, "Locations", DispatchProxy.Create<ILocationCheckHelper, ClientSessionTestProxy>());
        SetSessionHelper(session, "DataStorage", DispatchProxy.Create<IDataStorageHelper, ClientSessionTestProxy>());
        AttachConnection(client, session);
        AttachState(client, state);
        return session;
    }

    public static void Initialize(
        ArchipelagoClient client,
        ArchipelagoSession session,
        Dictionary<string, object> slotData,
        string seed,
        Queue<Action>? pending = null
    )
    {
        var connection = Connection(client);
        var attempt = new ClientConnection.Attempt(session, connection.Generation, "localhost:1", "Player1");
        if (pending != null)
            foreach (var action in pending)
                attempt.PendingEvents.Enqueue(action);
        SetField(connection, "_attempt", attempt);
        typeof(ArchipelagoClient)
            .GetMethod("InitializeSession", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(
                client,
                [
                    attempt,
                    new LoginSuccessful(
                        new ConnectedPacket
                        {
                            Team = 0,
                            Slot = 1,
                            SlotData = slotData,
                        }
                    ),
                    seed,
                ]
            );
    }

    public static ClientConnection Connection(ArchipelagoClient client) =>
        (ClientConnection)GetField(client, "_connection");

    public static ClientProgress Progress(ArchipelagoClient client) => (ClientProgress)GetField(client, "_progress");

    public static void AttachState(ArchipelagoClient client, ApState state)
    {
        SetField(client, "_state", state);
        var settings = GetField(client, "_settings") as ClientSlotSettings;
        if (settings == null)
        {
            var data = ClientSlotSettingsTests.ValidSlotData();
            data["starting_difficulty"] = 0L;
            settings = ClientSlotSettings.Read(data);
            SetField(client, "_settings", settings);
        }
        Progress(client).Initialize(state, settings);
    }

    public static void SetSettings(ArchipelagoClient client, string name, object value)
    {
        var settings = GetField(client, "_settings") as ClientSlotSettings;
        if (settings == null)
        {
            settings = ClientSlotSettings.Read(ClientSlotSettingsTests.ValidSlotData());
            SetField(client, "_settings", settings);
        }
        typeof(ClientSlotSettings)
            .GetField($"<{name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(settings, value);
    }

    public static void AttachConnection(ArchipelagoClient client, ArchipelagoSession session, bool loggedIn = true)
    {
        var connection = Connection(client);
        SetField(
            connection,
            "_attempt",
            new ClientConnection.Attempt(session, connection.Generation, "localhost:1", "Player1")
        );
        SetLoggedIn(client, loggedIn);
    }

    public static void SetLoggedIn(ArchipelagoClient client, bool loggedIn) =>
        typeof(ClientConnection).GetProperty(nameof(ClientConnection.LoggedIn))!.SetValue(Connection(client), loggedIn);

    public static object GetField(object target, string name) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    public static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    public static void SetProperty(ArchipelagoClient client, string name, object value) =>
        typeof(ArchipelagoClient).GetProperty(name)!.SetValue(client, value);

    private static void SetSessionHelper(ArchipelagoSession session, string name, object helper) =>
        typeof(ArchipelagoSession)
            .GetField($"<{name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(session, helper);
}

public class ClientSessionTestProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        targetMethod?.Name switch
        {
            "get_ActivePlayer" => new PlayerInfo(0, 1, "Player1", "Player1", ArchipelagoClient.Game, [], []),
            "GetPlayerAlias" => $"Player{args![0]}",
            "GetItemName" => $"Item {args![0]}",
            "GetLocationNameFromId" => $"Location {args![0]}",
            "CompleteLocationChecksAsync" => Task.CompletedTask,
            "ScoutLocationsAsync" => Task.FromResult(new Dictionary<long, ScoutedItemInfo>()),
            "TrackHints" => null,
            _ => throw new InvalidOperationException("Unexpected test session call: " + targetMethod?.Name),
        };
}
