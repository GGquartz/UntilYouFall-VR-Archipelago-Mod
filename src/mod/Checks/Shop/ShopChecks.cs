namespace UntilYouFallArchipelago;

internal static class ShopChecks
{
    public const long BaseId = LocationDefinitions.ShopBaseId;
    public const int Sections = LocationDefinitions.ShopSections;
    public const int SlotsPerSection = LocationDefinitions.ShopSlotsPerSection;
    public static readonly int[] Prices = LocationDefinitions.ShopPrices;

    public static string Name(int section, int slot)
    {
        Validate(section, slot);

        return $"Shop Item {section}-{slot}";
    }

    public static long LocationId(int section, int slot)
    {
        Validate(section, slot);

        return BaseId + (section - 1) * SlotsPerSection + slot;
    }

    public static int Price(int section) => Prices[section - 1];

    public static bool IsUnlocked(int section, int unlockCount) => section - 1 <= unlockCount;

    public static long[] AllIds() =>
        Enumerable
            .Range(1, Sections)
            .SelectMany(section => Enumerable.Range(1, SlotsPerSection).Select(slot => LocationId(section, slot)))
            .ToArray();

    private static void Validate(int section, int slot)
    {
        if (section is < 1 or > Sections)
            throw new ArgumentOutOfRangeException(nameof(section));
        if (slot is < 1 or > SlotsPerSection)
            throw new ArgumentOutOfRangeException(nameof(slot));
    }
}
