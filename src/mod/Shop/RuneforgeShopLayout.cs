namespace UntilYouFallArchipelago;

internal readonly record struct Float3(float X, float Y, float Z);

internal readonly record struct RackPose(Float3 Position, Float3 Rotation, Float3 Scale);

internal static class RuneforgeShopLayout
{
    private static readonly Float3 One = new(1f, 1f, 1f);

    public static readonly RackPose[] Sections =
    [
        new(new(5.8299494f, 0.5398625f, 15.921898f), new(0f, 250.00009f, 0f), One),
        new(new(6.114315f, 0.5398625f, 14.113333f), new(0f, 270.00012f, 0f), One),
        new(new(5.8787875f, 0.5398625f, 12.243811f), new(0f, 295.00012f, 0f), One),
        new(new(5.0438704f, 0.5398625f, 10.653278f), new(0f, 315.00012f, 0f), One),
        new(new(3.7003696f, 0.5398625f, 9.309791f), new(0f, 315.00012f, 0f), One),
    ];

    public static readonly RackPose[] FrontColumns =
    [
        new(new(0.19217037f, -0.05914995f, 0.37635526f), new(0f, 0f, 0f), new(1f, 0.8f, 1f)),
        new(new(-0.17683025f, -0.05914995f, 0.3763549f), new(0f, 0f, 0f), new(1f, 0.8f, 1f)),
    ];
}
