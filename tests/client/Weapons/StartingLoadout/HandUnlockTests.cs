using UntilYouFallArchipelago;

internal static class HandUnlockTests
{
    public static void Run()
    {
        var none = Array.Empty<long>();
        Assert(
            HandUnlockRules.IsUnlocked(StartingHands.Both, none, true)
                && HandUnlockRules.IsUnlocked(StartingHands.Both, none, false),
            "both starts with both hands"
        );
        Assert(
            HandUnlockRules.IsUnlocked(StartingHands.LeftOnly, none, true)
                && !HandUnlockRules.IsUnlocked(StartingHands.LeftOnly, none, false),
            "left_only locks only right"
        );
        Assert(
            !HandUnlockRules.IsUnlocked(StartingHands.RightOnly, none, true)
                && HandUnlockRules.IsUnlocked(StartingHands.RightOnly, none, false),
            "right_only locks only left"
        );
        Assert(
            HandUnlockRules.IsUnlocked(StartingHands.LeftOnly, new[] { ApItems.RightHandId }, false),
            "Right Hand unlocks the missing right hand"
        );
        Assert(
            HandUnlockRules.IsUnlocked(StartingHands.RightOnly, new[] { ApItems.LeftHandId }, true),
            "Left Hand unlocks the missing left hand"
        );
        Assert(
            !HandUnlockRules.IsUnlocked(StartingHands.LeftOnly, new[] { ApItems.LeftHandId }, false)
                && !HandUnlockRules.IsUnlocked(StartingHands.RightOnly, new[] { ApItems.RightHandId }, true),
            "the opposite hand item cannot unlock a locked hand"
        );
        Assert(
            HandUnlockRules.BlocksGrab(
                startingHands: StartingHands.RightOnly,
                unlocks: none,
                left: true,
                hasInteractable: true
            )
                && !HandUnlockRules.BlocksGrab(
                    startingHands: StartingHands.RightOnly,
                    unlocks: none,
                    left: false,
                    hasInteractable: true
                ),
            "a locked hand blocks every grab only on that side"
        );
        Assert(
            !HandUnlockRules.BlocksGrab(
                startingHands: StartingHands.RightOnly,
                unlocks: none,
                left: true,
                hasInteractable: false
            ),
            "an empty grab attempt is ignored without a locked-hand notice"
        );
        Assert(
            !HandUnlockRules.BlocksGrab(
                startingHands: StartingHands.RightOnly,
                unlocks: new[] { ApItems.LeftHandId },
                left: true,
                hasInteractable: true
            ),
            "the hand item permits grabs"
        );
        Assert(
            HandUnlockRules.ResolveIsLeftHand(
                hand: (nint)30,
                state: 0,
                liveLeft: (nint)30,
                liveRight: (nint)40,
                liveLeftState: 0,
                liveRightState: 0,
                cachedLeft: (nint)10,
                cachedRight: (nint)20,
                cachedLeftState: 0,
                cachedRightState: 0
            ) == true,
            "the live scene inventory identifies a newly created left hand before stale cached hands"
        );
        Assert(
            HandUnlockRules.ResolveIsLeftHand(
                hand: (nint)40,
                state: 0,
                liveLeft: (nint)30,
                liveRight: (nint)40,
                liveLeftState: 0,
                liveRightState: 0,
                cachedLeft: (nint)10,
                cachedRight: (nint)20,
                cachedLeftState: 0,
                cachedRightState: 0
            ) == false,
            "the live scene inventory identifies a newly created right hand"
        );
        Assert(
            HandUnlockRules.ResolveIsLeftHand(
                hand: (nint)10,
                state: 0,
                liveLeft: 0,
                liveRight: 0,
                liveLeftState: 0,
                liveRightState: 0,
                cachedLeft: (nint)10,
                cachedRight: (nint)20,
                cachedLeftState: 0,
                cachedRightState: 0
            ) == true,
            "cached hands remain a fallback while the live inventory is unavailable"
        );
        Assert(
            HandUnlockRules.ResolveIsLeftHand(
                hand: (nint)50,
                state: (nint)500,
                liveLeft: 0,
                liveRight: 0,
                liveLeftState: (nint)500,
                liveRightState: (nint)600,
                cachedLeft: (nint)10,
                cachedRight: (nint)20,
                cachedLeftState: (nint)100,
                cachedRightState: (nint)200
            ) == true,
            "a new-run hand is identified by its live state when PlayerHand references are stale"
        );
        Assert(
            HandUnlockRules.ResolveIsLeftHand(
                hand: (nint)60,
                state: (nint)600,
                liveLeft: 0,
                liveRight: 0,
                liveLeftState: (nint)500,
                liveRightState: (nint)600,
                cachedLeft: (nint)10,
                cachedRight: (nint)20,
                cachedLeftState: (nint)100,
                cachedRightState: (nint)200
            ) == false,
            "a new-run right hand is identified by its live state"
        );
        Assert(
            HandUnlockRules.ResolveIsLeftHand(
                hand: (nint)10,
                state: 0,
                liveLeft: (nint)30,
                liveRight: (nint)10,
                liveLeftState: 0,
                liveRightState: 0,
                cachedLeft: (nint)10,
                cachedRight: (nint)20,
                cachedLeftState: 0,
                cachedRightState: 0
            ) == false,
            "live right-hand identity takes priority over a conflicting stale left-hand pointer"
        );
        foreach (var pointer in new nint[] { 0, 99 })
            Assert(
                HandUnlockRules.ResolveIsLeftHand(
                    hand: pointer,
                    state: 0,
                    liveLeft: 0,
                    liveRight: 0,
                    liveLeftState: 0,
                    liveRightState: 0,
                    cachedLeft: 0,
                    cachedRight: 0,
                    cachedLeftState: 0,
                    cachedRightState: 0
                ) == null,
                "missing or unrecognized hand pointers stay unknown rather than selecting a locked side"
            );

        Assert(
            HandUnlockRules.ShouldCarryStarter(startingHands: StartingHands.LeftOnly, left: true, rightDominant: true)
                && !HandUnlockRules.ShouldCarryStarter(
                    startingHands: StartingHands.LeftOnly,
                    left: false,
                    rightDominant: true
                ),
            "left_only places the starter on the left"
        );
        Assert(
            !HandUnlockRules.ShouldCarryStarter(
                startingHands: StartingHands.RightOnly,
                left: true,
                rightDominant: false
            )
                && HandUnlockRules.ShouldCarryStarter(
                    startingHands: StartingHands.RightOnly,
                    left: false,
                    rightDominant: false
                ),
            "right_only places the starter on the right"
        );
        Assert(
            !HandUnlockRules.ShouldCarryStarter(startingHands: StartingHands.Both, left: true, rightDominant: true)
                && HandUnlockRules.ShouldCarryStarter(
                    startingHands: StartingHands.Both,
                    left: false,
                    rightDominant: true
                )
                && HandUnlockRules.ShouldCarryStarter(
                    startingHands: StartingHands.Both,
                    left: true,
                    rightDominant: false
                ),
            "both preserves the dominant-hand starter choice"
        );
        Console.WriteLine("PASS: locked-hand ownership, all-grab gate and configured starter side");
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok)
            throw new Exception("Hand unlocks: " + message);
    }
}
