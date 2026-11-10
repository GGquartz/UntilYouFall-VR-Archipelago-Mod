namespace Il2CppSG.Claymore.Armaments
{
    public class ArmamentVisualPreview { }
}

namespace UntilYouFallArchipelago
{
    internal sealed class TestItemVisual : IItemVisual
    {
        public void UseUnownedStyle(Il2CppSG.Claymore.Armaments.ArmamentVisualPreview style) { }

        public void Dispose() { }
    }

    internal static class ApLogoCrushableVisual
    {
        public static readonly IItemVisual Visual = new TestItemVisual();

        public static IItemVisual TryAttach(
            Il2CppSG.Claymore.Interaction.RewardInteractable reward,
            Action<string> log
        ) => Visual;
    }

    internal static class NativeModelCrushableVisual
    {
        public static readonly IItemVisual Visual = new TestItemVisual();
        public static bool Failed;
        public static bool Waiting;

        public static bool HasFailed(string resource) => Failed;

        public static IItemVisual? TryAttach(
            Il2CppSG.Claymore.Interaction.RewardInteractable reward,
            string resource,
            Action<string> log
        ) => Waiting ? null : Visual;
    }
}
