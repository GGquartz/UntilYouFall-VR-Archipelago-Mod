namespace UntilYouFallArchipelago;

internal static class AetherFillerRules
{
    public const int Amount = ApItems.AetherFillerAmount;

    public static bool CanApply(bool connected, bool hasCurrency) => connected && hasCurrency;

    public static int Apply(int currentAether) => currentAether + Amount;

    public static int Apply(int currentAether, int amount) => currentAether + amount;
}
