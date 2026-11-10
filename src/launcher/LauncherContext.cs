namespace UntilYouFall.Launcher;

internal sealed class LauncherContext(string payload, CancellationToken lifetime, Action<string> log)
{
    public string Payload { get; } = payload;
    public CancellationToken Lifetime { get; } = lifetime;
    public Action<string> Log { get; } = log;

    public Func<string> GameDirectoryText { get; set; } = () => "";
    public Func<string> GetGameDirectory { get; set; } = () => "";
    public Func<Task> RefreshVersions { get; set; } = () => Task.CompletedTask;
}
