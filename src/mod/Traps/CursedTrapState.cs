namespace UntilYouFallArchipelago;

internal sealed class CursedTrapState : TimedTrapState
{
    public float ScaleDamage(float amount) => Active && InRun && amount > 0f ? amount * 2f : amount;
}
