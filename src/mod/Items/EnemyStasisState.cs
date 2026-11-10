namespace UntilYouFallArchipelago;

internal sealed class EnemyStasisState : TimedTrapState
{
    public const int EffectSeconds = 10;
    public bool Pending { get; private set; }

    public bool Receive(bool ready)
    {
        if (!InRun || !ready)
            return false;
        if (!RoomCleared)
            return Activate(true);

        EndStage();
        Pending = true;

        return true;
    }

    public bool StartPending(bool ready) => Pending && !RoomCleared && Activate(ready);

    public override bool Activate(bool ready, int durationSeconds = DefaultDurationSeconds)
    {
        if (!InRun || !ready || RoomCleared)
            return false;

        Pending = false;
        Start(EffectSeconds);

        return true;
    }

    public override bool EndStage()
    {
        Pending = false;
        return base.EndStage();
    }
}
