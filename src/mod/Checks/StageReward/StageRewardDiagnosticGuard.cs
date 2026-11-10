namespace UntilYouFallArchipelago;

internal static class StageRewardDiagnosticGuard
{
    public static void Run(Action action, Action<Exception> onError)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            onError(exception);
        }
    }
}
