namespace UntilYouFallArchipelago;

internal static class ClosedCaptionFixRules
{
    public static bool ShouldEnableDrawEffect(bool requested, bool uiActive, int visibleCharacters) =>
        requested && uiActive && visibleCharacters > 0;
}
