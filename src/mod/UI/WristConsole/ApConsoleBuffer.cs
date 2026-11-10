namespace UntilYouFallArchipelago;

internal sealed class ApConsoleBuffer
{
    private readonly Queue<ApConsoleMessage> _messages = new();
    public string RichText => string.Join("\n", _messages.Select(message => message.RichText));

    public void Add(ApConsoleMessage message)
    {
        _messages.Enqueue(message);
        while (_messages.Count > 40)
            _messages.Dequeue();
    }

    public bool DropOldest()
    {
        if (_messages.Count <= 1)
            return false;

        _messages.Dequeue();
        return true;
    }
}
