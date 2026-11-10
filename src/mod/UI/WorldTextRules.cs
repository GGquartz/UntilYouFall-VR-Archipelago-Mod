namespace UntilYouFallArchipelago;

internal static class WorldTextRules
{
    public static int FirstVisibleLayer(int cullingMask)
    {
        for (var layer = 0; layer < 32; layer++)
            if ((cullingMask & (1 << layer)) != 0)
                return layer;

        return -1;
    }
}
