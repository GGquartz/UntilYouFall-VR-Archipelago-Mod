using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Models;

namespace UntilYouFallArchipelago;

internal sealed class ClientNotifications
{
    private readonly LogChannel _log;
    private readonly LogChannel _noticeLog;
    private readonly Action<ApNotice>? _notify;
    private readonly SortedDictionary<int, NetworkItem> _received = [];
    private readonly HashSet<long> _sent = [];

    public ClientNotifications(Action<string> log, Action<ApNotice>? notify)
    {
        _log = log.Channel("AP");
        _noticeLog = log.Channel("APNotice");
        _notify = notify;
    }

    public void QueueReceived(int index, NetworkItem item) => _received[index] = item;

    public void PruneReceived(int notifiedCount)
    {
        foreach (var index in _received.Keys.Where(index => index < notifiedCount).ToArray())
            _received.Remove(index);
    }

    public void ClearReceived() => _received.Clear();

    public void Reset()
    {
        _received.Clear();
        _sent.Clear();
    }

    public void Notify(ApNotice notice)
    {
        _noticeLog.Write("" + notice.PlainText);
        _notify?.Invoke(notice);
    }

    public void NotifySent(long locationId, ApNotice notice)
    {
        if (_sent.Add(locationId))
            Notify(notice);
    }

    public void DeliverReceived(ArchipelagoSession session, ApState state, Action disconnect)
    {
        if (_notify == null)
            return;

        while (_received.TryGetValue(state.NotifiedReceivedCount, out var receipt))
        {
            var index = state.NotifiedReceivedCount;
            try
            {
                state.RecordReceivedNotice(index);
            }
            catch (Exception ex)
            {
                _log.Write("Cannot persist notification cursor: " + ex.Message);
                disconnect();
                break;
            }
            _received.Remove(index);
            if (
                receipt.Location == -2
                && (
                    WeaponCatalog.ById(receipt.Item) != null
                    || ApItems.IsHand(receipt.Item)
                    || DifficultySettings.IsItem(receipt.Item)
                )
            )
                continue;

            var name = session.Items.GetItemName(receipt.Item) ?? $"Item: {receipt.Item}";
            var sender = receipt.Player == 0 ? "Server" : session.Players.GetPlayerAlias(receipt.Player);
            var notice =
                receipt.Player == session.Players.ActivePlayer.Slot && receipt.Location >= 0
                    ? ApNotice
                        .Found(name, receipt.Flags)
                        .AtLocation(
                            session.Locations.GetLocationNameFromId(receipt.Location) ?? $"Location: {receipt.Location}"
                        )
                    : ApNotice.Received(name, receipt.Flags, sender);

            Notify(notice);
        }
    }
}
