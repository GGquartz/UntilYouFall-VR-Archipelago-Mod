using Il2CppSG.Claymore.Interaction;

namespace UntilYouFallArchipelago;

internal interface IItemVisual : IDisposable
{
    void UseUnownedStyle(Il2CppSG.Claymore.Armaments.ArmamentVisualPreview style);
}

internal static class ItemVisuals
{
    public static IItemVisual? TryAttach(RewardInteractable reward, StageRewardItemInfo? info, Action<string> log)
    {
        if (info == null)
            return null;

        var resource = ItemVisualModels.Resource(info.Game, info.ItemName);
        if (resource == null || NativeModelCrushableVisual.HasFailed(resource))
            return ApLogoCrushableVisual.TryAttach(reward, log);
        return NativeModelCrushableVisual.TryAttach(reward, resource, log);
    }
}
