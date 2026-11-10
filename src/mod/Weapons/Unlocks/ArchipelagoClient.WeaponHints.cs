using Archipelago.MultiClient.Net.MessageLog.Messages;

namespace UntilYouFallArchipelago;

internal sealed partial class ArchipelagoClient
{
    private long _weaponHintPendingUntil;
    private long _weaponHintRepliesUntil;
    private readonly HashSet<long> _weaponHintKnown = [];
    private bool _weaponHintSent;
    private ApConsoleMessage? _weaponHintReply;
    private ApConsoleMessage? _weaponHintStatus;
    public bool WeaponHintsEnabled { get; private set; }

    private bool WeaponHintPending => Volatile.Read(ref _weaponHintPendingUntil) != 0;

    public bool CanHintWeapon =>
        Connected
        && WeaponHintsEnabled
        && _weaponHintPendingUntil == 0
        && Environment.TickCount64 >= _weaponHintRepliesUntil;

    public bool HintNextWeapon()
    {
        if (!Connected || !WeaponHintsEnabled || !CanHintWeapon)
            return false;

        ResetWeaponHintRequest();
        _weaponHintPendingUntil = Environment.TickCount64 + 10000;
        var deadline = _weaponHintPendingUntil;
        var generation = _connection.Generation;
        var session = _connection.Session!;
        void PostIfCurrent(Action action) =>
            PostSessionEvent(
                generation,
                () =>
                {
                    if (Connected && _weaponHintPendingUntil == deadline)
                        action();
                }
            );
        _ = Task.Run(async () =>
        {
            try
            {
                var hints = await session.DataStorage.GetHintsAsync();
                PostIfCurrent(() =>
                {
                    try
                    {
                        _weaponHintKnown.UnionWith(
                            hints
                                .Where(hint => hint.ReceivingPlayer == session.Players.ActivePlayer.Slot)
                                .Select(hint => hint.ItemId)
                        );
                        _weaponHintSent = true;
                        session.Say("!hint Weapons");
                        _weaponHintLog.Write("Requested !hint Weapons");
                    }
                    catch (Exception exception)
                    {
                        WeaponHintFailed(exception);
                    }
                });
            }
            catch (Exception exception)
            {
                PostIfCurrent(() => WeaponHintFailed(exception));
            }
        });

        return true;
    }

    private void ReceiveWeaponHint(LogMessage message)
    {
        if (!Connected || _weaponHintPendingUntil == 0 || !_weaponHintSent)
            return;

        var weaponHint =
            message is HintItemSendLogMessage hint
            && hint.IsReceiverTheActivePlayer
            && !hint.IsFound
            && WeaponCatalog.ById(hint.Item.ItemId) != null
            && !_weaponHintKnown.Contains(hint.Item.ItemId);
        var serverReply =
            !weaponHint
            && (message is CommandResultLogMessage or ServerChatLogMessage || message.GetType() == typeof(LogMessage))
            && IsWeaponHintStatus(message.ToString());
        if (!weaponHint && !serverReply)
            return;
        var entry = ApConsoleMessage.FromLog(message);
        if (weaponHint)
            _weaponHintReply = entry;
        else
            _weaponHintStatus = entry;

        _weaponHintRepliesUntil = Environment.TickCount64 + 250;
    }

    private void UpdateWeaponHint()
    {
        if (_weaponHintPendingUntil == 0)
            return;
        if (!Connected)
        {
            ResetWeaponHintRequest();
            return;
        }
        if (_weaponHintRepliesUntil != 0 && Environment.TickCount64 >= _weaponHintRepliesUntil)
        {
            var reply = (_weaponHintReply ?? _weaponHintStatus)!;
            _weaponHintLog.Write($"Reply; {reply.PlainText}");
            ResetWeaponHintRequest();
            Notify(new ApNotice(reply.PlainText, reply.RichText));
            return;
        }
        if (Environment.TickCount64 < _weaponHintPendingUntil)
            return;

        ResetWeaponHintRequest();
        Notify(ApNotice.Plain("No weapon hint reply received"));
    }

    internal static bool IsWeaponHintStatus(string text) =>
        text.StartsWith("You can't afford the hint.", StringComparison.Ordinal)
        || text.StartsWith("Hint was previously used", StringComparison.Ordinal)
        || text.StartsWith("Nothing found for recognized item name", StringComparison.Ordinal)
        || text.StartsWith("There may be more hintables, however, you cannot afford", StringComparison.Ordinal);

    private void WeaponHintFailed(Exception exception)
    {
        ResetWeaponHintRequest();
        _weaponHintLog.Write("Request failed: " + exception);
        Notify(ApNotice.Plain("Weapon hint request failed"));
    }

    private void ResetWeaponHintRequest()
    {
        _weaponHintPendingUntil = 0;
        _weaponHintRepliesUntil = 0;
        _weaponHintSent = false;
        _weaponHintKnown.Clear();
        _weaponHintReply = null;
        _weaponHintStatus = null;
    }
}
