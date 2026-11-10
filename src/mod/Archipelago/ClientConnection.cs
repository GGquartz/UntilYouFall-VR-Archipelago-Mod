using System.Collections.Concurrent;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using UntilYouFall.Shared;

namespace UntilYouFallArchipelago;

internal sealed class ClientConnection
{
    internal sealed class Attempt(ArchipelagoSession session, int generation, string address, string slot)
    {
        public ArchipelagoSession Session { get; } = session;
        public int Generation { get; } = generation;
        public string Address { get; } = address;
        public string Slot { get; } = slot;
        public CancellationTokenSource Cancellation { get; } = new();
        public Queue<Action> PendingEvents { get; } = new();
    }

    private readonly LogChannel _log;
    private readonly Action<ConnectionState, string> _report;
    private readonly Action _disconnect;
    private readonly ConcurrentQueue<Action> _events = new();
    private Attempt? _attempt;
    public int Generation { get; private set; }
    public bool LoggedIn { get; private set; }
    public ArchipelagoSession? Session => _attempt?.Session;
    public bool Connected => LoggedIn && Session?.Socket.Connected == true;

    public ClientConnection(Action<string> log, Action<ConnectionState, string> report, Action disconnect)
    {
        _log = log.Channel("AP");
        _report = report;
        _disconnect = disconnect;
    }

    public Attempt Begin(string address, string slot)
    {
        _report(ConnectionState.Connecting, $"Connecting to {address} as {slot}...");
        var generation = ++Generation;
        var session = ArchipelagoSessionFactory.CreateSession(address);
        _attempt = new Attempt(session, generation, address, slot);
        LoggedIn = false;

        return _attempt;
    }

    public void Post(int generation, Action action) =>
        _events.Enqueue(() =>
        {
            if (Generation == generation)
                action();
        });

    public void ProcessEvents()
    {
        while (_events.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _log.Write("Processing stopped: " + ex.Message);
                _report(ConnectionState.Error, ex.GetType().Name + ": " + ex.Message);
                _disconnect();
            }
        }
    }

    public void CompleteLogin(Attempt attempt)
    {
        LoggedIn = true;
        while (attempt.PendingEvents.TryDequeue(out var apply))
            apply();
    }

    public void MarkSocketClosed() => LoggedIn = false;

    public void Login(Attempt attempt, string password, Action<LoginSuccessful, string> initialize)
    {
        void PostResult(Action action) => Post(attempt.Generation, action);

        try
        {
            if (attempt.Cancellation.IsCancellationRequested)
                return;

            var login = attempt.Session.TryConnectAndLogin(
                ArchipelagoClient.Game,
                attempt.Slot,
                ItemsHandlingFlags.AllItems,
                new Version(0, 6, 7),
                uuid: "UYF-" + Environment.MachineName,
                password: string.IsNullOrEmpty(password) ? null : password
            );
            if (login is not LoginSuccessful success)
            {
                var message = string.Join("; ", ((LoginFailure)login).Errors);
                PostResult(() =>
                {
                    _log.Write("Login failed: " + message);
                    _report(ConnectionState.Error, "Login rejected: " + message);
                    _disconnect();
                });
                return;
            }
            var seed = attempt.Session.RoomState.Seed;
            PostResult(() => initialize(success, seed));
        }
        catch (Exception ex)
        {
            PostResult(() =>
            {
                _log.Write("Connection failed: " + ex.Message);
                _report(ConnectionState.Error, ex.GetType().Name + ": " + ex.Message);
                _disconnect();
            });
        }
        finally
        {
            if (attempt.Cancellation.IsCancellationRequested)
                _ = attempt.Session.Socket.DisconnectAsync();
        }
    }

    public void Disconnect()
    {
        ++Generation;
        LoggedIn = false;
        _attempt?.Cancellation.Cancel();
        var session = Session;
        _attempt = null;
        if (session != null)
            _ = session.Socket.DisconnectAsync();
    }
}
