namespace UntilYouFallArchipelago;

internal sealed class TechnicalNoticeGate
{
    private bool _reserved;

    public bool TryReserve()
    {
        if (_reserved)
            return false;
        _reserved = true;
        return true;
    }

    public void Release() => _reserved = false;
}
