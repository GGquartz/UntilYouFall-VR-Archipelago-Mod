namespace UntilYouFallArchipelago;

internal class TimedTrapState
{
    public const int DefaultDurationSeconds = OptionRanges.TrapEffectLengthDefault;
    public int DurationSeconds { get; private set; } = DefaultDurationSeconds;
    public double RemainingSeconds { get; private set; }
    public bool Active => RemainingSeconds > 0;
    public bool InRun { get; private set; }
    public bool RoomCleared { get; private set; }
    public virtual bool PauseBetweenRooms => true;

    public virtual bool Activate(bool ready, int durationSeconds = DefaultDurationSeconds)
    {
        if (!InRun || !ready)
            return false;

        ValidateDuration(durationSeconds);
        Start(durationSeconds);

        return true;
    }

    protected void Start(int durationSeconds)
    {
        DurationSeconds = durationSeconds;
        RemainingSeconds = DurationSeconds;
    }

    public static int ReadDuration(IDictionary<string, object> slotData)
    {
        if (!slotData.TryGetValue("trap_effect_length", out var raw) || raw == null)
            throw new InvalidDataException("Missing trap effect length in AP slot data");
        try
        {
            var duration = Convert.ToInt32(raw);
            if (Convert.ToDouble(raw) != duration)
                throw new ArgumentOutOfRangeException(nameof(raw));
            ValidateDuration(duration);

            return duration;
        }
        catch (Exception exception)
            when (exception
                    is FormatException
                        or InvalidCastException
                        or OverflowException
                        or ArgumentOutOfRangeException
            )
        {
            throw new InvalidDataException(
                $"Invalid trap effect length in AP slot data, expected {OptionRanges.TrapEffectLengthMinimum}–{OptionRanges.TrapEffectLengthMaximum} seconds",
                exception
            );
        }
    }

    private static void ValidateDuration(int durationSeconds)
    {
        if (durationSeconds is < OptionRanges.TrapEffectLengthMinimum or > OptionRanges.TrapEffectLengthMaximum)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
    }

    public bool Advance(double elapsedSeconds, bool paused)
    {
        if (!Active || paused || RoomCleared && PauseBetweenRooms)
            return false;
        RemainingSeconds = Math.Max(0, RemainingSeconds - elapsedSeconds);
        if (Active)
            return false;
        EndStage();
        return true;
    }

    public virtual bool EndStage()
    {
        var wasActive = Active;
        RemainingSeconds = 0;
        return wasActive;
    }

    public bool EndRun()
    {
        InRun = false;
        RoomCleared = false;
        return EndStage();
    }

    public void ClearRoom() => RoomCleared = true;

    public virtual void LoadRoom(bool combat)
    {
        InRun = combat;
        RoomCleared = false;
        if (!combat)
            EndStage();
    }
}
