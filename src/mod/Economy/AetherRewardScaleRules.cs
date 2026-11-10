namespace UntilYouFallArchipelago;

internal static class AetherRewardScaleRules
{
    public const int Shard = 100;
    public const int Fragment = 150;
    public const int Crystal = 200;
    public const int Cluster = 500;
    public const int Blossom = 1_000;

    public static int? ScaleFor(int vanillaBase) =>
        vanillaBase switch
        {
            15 => Shard,
            25 => Fragment,
            50 => Crystal,
            100 => Cluster,
            250 => Blossom,
            _ => null,
        };
}
