using Il2CppSG.Claymore.Interaction;
using Il2CppSG.Claymore.Level;

namespace UntilYouFallArchipelago;

internal static class LevelManagerHandExtensions
{
    public static IEnumerable<PlayerHand> ActiveHands(this LevelManager? manager)
    {
        var inventory = manager?.armamentInventory;

        foreach (var hand in new[] { inventory?.leftHand?.PlayerHand, inventory?.rightHand?.PlayerHand })
            if (hand != null && hand.isActiveAndEnabled && hand.gameObject.activeInHierarchy)
                yield return hand;
    }
}
