using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

internal static class LauncherResourceTests
{
    public static void Run(Action<string, bool> check)
    {
        AppBuilder
            .Configure<UntilYouFall.Launcher.App>()
            .UseStandardRuntimePlatformSubsystem()
            .UseWindowingSubsystem(() => { }, "Tests")
            .UseRenderingSubsystem(() => { }, "Tests")
            .SetupWithoutStarting();
        var styles = Application.Current!.Styles;
        foreach (
            var (key, color) in new[]
            {
                ("SecondaryTextBrush", "#969CAC"),
                ("SuccessBrush", "#6EC882"),
                ("ErrorBrush", "#EB6E64"),
            }
        )
        {
            var found = styles.TryGetResource(key, null, out var resource);
            check(
                "semantic brush keeps its color: " + key,
                found && resource is ISolidColorBrush brush && brush.Color == Color.Parse(color)
            );
            if (found)
                check(
                    "code resolves the shared brush: " + key,
                    ReferenceEquals(Application.Current.FindResource(key), resource)
                );
        }
        foreach (var key in new[] { "MutedBrush", "GoodBrush", "BadBrush" })
            check("old brush name removed: " + key, !styles.TryGetResource(key, null, out _));
    }
}
