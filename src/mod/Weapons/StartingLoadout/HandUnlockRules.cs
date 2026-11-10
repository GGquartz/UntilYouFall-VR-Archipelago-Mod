namespace UntilYouFallArchipelago;

internal static class HandUnlockRules
{
    public static bool IsUnlocked(StartingHands startingHands, IReadOnlyCollection<long> unlocks, bool left) =>
        startingHands == StartingHands.Both
        || startingHands == StartingHands.LeftOnly && (left || unlocks.Contains(ApItems.RightHandId))
        || startingHands == StartingHands.RightOnly && (!left || unlocks.Contains(ApItems.LeftHandId));

    public static bool BlocksGrab(
        StartingHands startingHands,
        IReadOnlyCollection<long> unlocks,
        bool left,
        bool hasInteractable
    ) => hasInteractable && !IsUnlocked(startingHands, unlocks, left);

    public static bool? ResolveIsLeftHand(
        nint hand,
        nint state,
        nint liveLeft,
        nint liveRight,
        nint liveLeftState,
        nint liveRightState,
        nint cachedLeft,
        nint cachedRight,
        nint cachedLeftState,
        nint cachedRightState
    )
    {
        if (liveLeft != 0 && hand == liveLeft)
            return true;
        if (liveRight != 0 && hand == liveRight)
            return false;
        if (liveLeftState != 0 && state == liveLeftState)
            return true;
        if (liveRightState != 0 && state == liveRightState)
            return false;
        if (cachedLeft != 0 && hand == cachedLeft)
            return true;
        if (cachedRight != 0 && hand == cachedRight)
            return false;
        if (cachedLeftState != 0 && state == cachedLeftState)
            return true;
        if (cachedRightState != 0 && state == cachedRightState)
            return false;

        return null;
    }

    public static bool ShouldCarryStarter(StartingHands startingHands, bool left, bool rightDominant) =>
        startingHands switch
        {
            StartingHands.LeftOnly => left,
            StartingHands.RightOnly => !left,
            _ => left != rightDominant,
        };
}
