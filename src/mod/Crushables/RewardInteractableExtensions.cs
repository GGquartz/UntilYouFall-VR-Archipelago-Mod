using Il2CppSG.Claymore.Interaction;

namespace UntilYouFallArchipelago;

internal static class RewardInteractableExtensions
{
    public static void ReleaseFromHand(this RewardInteractable? reward)
    {
        if (reward == null)
            return;

        var hand = reward.ActiveHand;
        if (hand != null && hand.HeldInteractable?.Pointer == reward.Pointer)
            hand.ForceGrabEnd();
    }
}
