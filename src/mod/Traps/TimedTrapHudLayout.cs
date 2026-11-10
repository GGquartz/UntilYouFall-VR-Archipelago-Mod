namespace UntilYouFallArchipelago;

internal static class TimedTrapHudLayout
{
    public const int DodgeRow = 0;

    public static int ExhaustionRow(bool dodgeActive) => dodgeActive ? 1 : 0;

    public static int CursedRow(bool dodgeActive, bool exhaustionActive) =>
        (dodgeActive ? 1 : 0) + (exhaustionActive ? 1 : 0);

    public static int StasisRow(bool dodgeActive, bool exhaustionActive, bool cursedActive) =>
        CursedRow(dodgeActive, exhaustionActive) + (cursedActive ? 1 : 0);

    public static float Height(int row) => 0.27f + row * 0.14f;
}
