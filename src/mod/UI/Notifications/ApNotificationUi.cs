namespace UntilYouFallArchipelago;

internal sealed class ApNotificationUi : IDisposable
{
    private const long DisplayMilliseconds = 7000;

    private const int RushQueueSize = 6;
    private const long RushDisplayMilliseconds = 1000;

    private readonly List<QueuedNotice> _queue = [];
    private readonly TechnicalNoticeGate _technicalGate = new();
    private readonly LogChannel _log;
    private readonly LogChannel _technicalUILog;
    private readonly NoticeSound _sound;
    private readonly VrNotificationPanel _panel;
    private readonly VrNotificationPanel _technicalPanel;
    private ApNotice? _technicalNotice;
    private long _hideAt;
    private long _shownAt;
    private QueuedNotice? _shown;
    private long _retryAt;
    private bool _rushing;
    private bool _showing;
    private long _diagnoseAt;
    private long _technicalHideAt;
    private long _technicalResumeMilliseconds;
    private long _technicalRetryAt;
    private bool _technicalShowing;
    private long _technicalDiagnoseAt;

    public ApNotificationUi(Action<string> log)
    {
        _log = log.Channel("APNoticeUI");
        _technicalUILog = log.Channel("APTechnicalUI");
        _sound = new NoticeSound(log);
        _panel = new VrNotificationPanel(
            log,
            "Archipelago Notification",
            "APNoticeUI",
            NotificationPanelLayout.ApVerticalPosition
        );
        _technicalPanel = new VrNotificationPanel(
            log,
            "Archipelago Technical Notification",
            "APTechnicalUI",
            NotificationPanelLayout.TechnicalVerticalPosition
        );
    }

    public void Enqueue(ApNotice notice)
    {
        var entry = new QueuedNotice(notice);
        if (!notice.IsUrgent)
        {
            _queue.Add(entry);
            return;
        }
        var index = 0;
        while (index < _queue.Count && _queue[index].Notice.IsUrgent)
            index++;

        _queue.Insert(index, entry);
    }

    public void ShowTechnical(ApNotice notice)
    {
        if (_technicalGate.TryReserve())
            _technicalNotice = notice;
    }

    public void Update()
    {
        if (_queue.Count == 0)
            _rushing = false;
        else if (_queue.Count >= RushQueueSize)
            _rushing = true;

        if (!_showing && !_technicalShowing && _queue.Count == 0 && _technicalNotice == null)
            return;

        var now = Environment.TickCount64;
        UpdateAp(now);
        UpdateTechnical(now);
    }

    private void UpdateAp(long now)
    {
        try
        {
            if (_showing && _rushing)
                _hideAt = Math.Min(_hideAt, _shownAt + RushDisplayMilliseconds);
            if (_showing && !_panel.IsReady)
            {
                _log.Write("Presentation interrupted");
                _shown!.RemainingMilliseconds = RemainingMilliseconds(_hideAt, now);
                _panel.Dispose();
                _showing = false;
                _diagnoseAt = 0;
                _retryAt = now + 500;
            }
            if (_showing && _diagnoseAt != 0 && now >= _diagnoseAt)
            {
                _diagnoseAt = 0;
                try
                {
                    _panel.LogRenderState();
                }
                catch (Exception ex)
                {
                    _log.Write("RenderState unavailable: " + ex.Message);
                }
            }
            if (_showing && !ReferenceEquals(_queue[0], _shown))
            {
                _shown!.RemainingMilliseconds = RemainingMilliseconds(_hideAt, now);
                _panel.Hide();
                _showing = false;
                _retryAt = 0;
            }
            if (_showing && now >= _hideAt)
            {
                _panel.Hide();
                _queue.Remove(_shown!);
                if (_queue.Count == 0)
                    _rushing = false;
                _showing = false;
                _retryAt = now + 500;
            }
            if (_showing || _queue.Count == 0 || now < _retryAt)
                return;
            if (!_panel.IsReady)
            {
                _panel.Dispose();
                if (!_panel.TryCreate())
                {
                    _retryAt = now + 5000;
                    return;
                }
            }

            var entry = _queue[0];
            _panel.Show(entry.Notice);
            if (!entry.SoundPlayed)
            {
                entry.SoundPlayed = true;
                _sound.Play(entry.Notice);
            }
            _shown = entry;
            _showing = true;
            _shownAt = now;
            _hideAt = now + entry.RemainingMilliseconds;
            _diagnoseAt = now + 500;

            _log.Write($"Showing: {entry.Notice.PlainText}");
        }
        catch (Exception ex)
        {
            _log.Write("Retrying: " + ex.Message);
            if (_showing)
                _shown!.RemainingMilliseconds = RemainingMilliseconds(_hideAt, now);
            _panel.Dispose();
            _showing = false;
            _retryAt = now + 10000;
        }
    }

    private void UpdateTechnical(long now)
    {
        try
        {
            if (_technicalShowing && !_technicalPanel.IsReady)
            {
                _technicalUILog.Write("Presentation interrupted");
                _technicalPanel.LogRenderState();
                _technicalResumeMilliseconds = RemainingMilliseconds(_technicalHideAt, now);
                _technicalPanel.Dispose();
                _technicalShowing = false;
                _technicalDiagnoseAt = 0;
                _technicalRetryAt = now + 500;
            }
            if (_technicalShowing && _technicalDiagnoseAt != 0 && now >= _technicalDiagnoseAt)
            {
                _technicalDiagnoseAt = now + 2000;
                try
                {
                    _technicalPanel.LogRenderState();
                }
                catch (Exception ex)
                {
                    _technicalUILog.Write("RenderState unavailable: " + ex.Message);
                }
            }
            if (_technicalShowing && now >= _technicalHideAt)
            {
                _technicalPanel.Hide();
                _technicalNotice = null;
                _technicalShowing = false;
                _technicalResumeMilliseconds = 0;
                _technicalGate.Release();
                _technicalRetryAt = now + 500;
            }
            if (_technicalShowing || _technicalNotice == null || now < _technicalRetryAt)
                return;
            if (!_technicalPanel.IsReady)
            {
                _technicalPanel.Dispose();
                if (!_technicalPanel.TryCreate())
                {
                    _technicalRetryAt = now + 5000;
                    return;
                }
            }

            var notice = _technicalNotice;
            _technicalPanel.Show(notice);
            _technicalShowing = true;
            _technicalHideAt =
                now + (_technicalResumeMilliseconds > 0 ? _technicalResumeMilliseconds : DisplayMilliseconds);
            _technicalResumeMilliseconds = 0;
            _technicalDiagnoseAt = now + 500;

            _technicalUILog.Write($"Showing: {notice.PlainText}");
        }
        catch (Exception ex)
        {
            _technicalUILog.Write("Retrying: " + ex.Message);
            if (_technicalShowing)
                _technicalResumeMilliseconds = RemainingMilliseconds(_technicalHideAt, now);
            _technicalPanel.Dispose();
            _technicalShowing = false;
            _technicalRetryAt = now + 10000;
        }
    }

    private sealed class QueuedNotice(ApNotice notice)
    {
        public ApNotice Notice { get; } = notice;
        public long RemainingMilliseconds { get; set; } = DisplayMilliseconds;
        public bool SoundPlayed { get; set; }
    }

    private static long RemainingMilliseconds(long hideAt, long now) => Math.Max(1, hideAt - now);

    public void Dispose()
    {
        _queue.Clear();
        _rushing = false;
        _shown = null;
        _technicalNotice = null;
        _technicalGate.Release();
        _panel.Dispose();
        _technicalPanel.Dispose();
    }
}
