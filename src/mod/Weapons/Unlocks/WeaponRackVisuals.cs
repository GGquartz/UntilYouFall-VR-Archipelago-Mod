using Il2CppSG.Claymore.Armory;

namespace UntilYouFallArchipelago;

internal static class WeaponRackVisuals
{
    public static void Refresh(WeaponRackSlot slot, bool locked)
    {
        if (!locked)
            return;

        if (slot.notPurchasedIndicator != null)
            slot.notPurchasedIndicator.SetActive(true);
        if (slot.purchasedIndicator != null)
            slot.purchasedIndicator.SetActive(false);
        foreach (var indicator in slot.levelIndicators)
            if (indicator != null)
                indicator.SetActive(false);
    }
}
