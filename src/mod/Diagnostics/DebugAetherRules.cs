namespace UntilYouFallArchipelago;

internal static class DebugAetherRules
{
    public const int GrantAmount = 50_000;

    public static bool ShouldGrant(bool enabled, bool isNewProfile) => enabled && isNewProfile;

    public static int Apply(int currentAether) => currentAether + GrantAmount;
}
