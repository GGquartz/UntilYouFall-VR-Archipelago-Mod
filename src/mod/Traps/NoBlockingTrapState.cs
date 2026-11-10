namespace UntilYouFallArchipelago;

internal sealed class NoBlockingTrapState : TimedTrapState
{
    private HashSet<(IntPtr Pointer, float StartedAt)> _existingProjectiles = [];

    public bool TryActivate(
        bool ready,
        IEnumerable<(IntPtr Pointer, float StartedAt)> existingProjectiles,
        int durationSeconds = DefaultDurationSeconds
    )
    {
        if (!InRun || !ready)
            return false;

        if (!Active)
            _existingProjectiles = existingProjectiles.ToHashSet();

        return Activate(ready, durationSeconds);
    }

    public bool ShouldForceDodge(bool samePlayer, bool ready) => Active && samePlayer && ready;

    public bool SuppressProjectileBlock(IntPtr projectile, float startedAt) =>
        Active && !_existingProjectiles.Contains((projectile, startedAt));

    public override bool EndStage()
    {
        _existingProjectiles.Clear();
        return base.EndStage();
    }

    public override void LoadRoom(bool combat)
    {
        base.LoadRoom(combat);
        if (combat)
            _existingProjectiles.Clear();
    }
}
