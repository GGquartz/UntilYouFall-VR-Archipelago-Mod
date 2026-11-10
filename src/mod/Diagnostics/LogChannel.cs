namespace UntilYouFallArchipelago;

internal sealed class LogChannel(
    Action<string> sink,
    string category,
    Action<string>? warning = null,
    Action<string>? error = null
)
{
    public Action<string> Sink { get; } = sink;

    public void Write(string message) => Sink.Write(category, message);

    public void Warn(string message) => (warning ?? Sink).Write(category, message);

    public void Error(string message) => (error ?? Sink).Write(category, message);
}

internal static class LogChannelExtensions
{
    public static LogChannel Channel(this Action<string> sink, string category) => new(sink, category);

    public static void Write(this Action<string> sink, string category, string message) =>
        sink($"[{category}] {message}");
}
