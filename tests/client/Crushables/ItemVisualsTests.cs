using Archipelago.MultiClient.Net.Enums;
using Il2CppSG.Claymore.Interaction;
using UntilYouFallArchipelago;

internal static class ItemVisualsTests
{
    public static void Run()
    {
        var reward = new RewardInteractable();
        Check(ItemVisuals.TryAttach(reward, null, _ => { }) == null, "unknown scout waits rather than freezing logo");
        var info = new StageRewardItemInfo(
            911601,
            "Mistcutter",
            "player",
            "Until You Fall",
            ItemFlags.Advancement,
            true
        );
        Check(
            ItemVisuals.TryAttach(reward, info, _ => { }) == NativeModelCrushableVisual.Visual,
            "later scout uses native model"
        );
        Check(
            ItemVisuals.TryAttach(reward, info with { Game = "Other Game" }, _ => { }) == ApLogoCrushableVisual.Visual,
            "foreign item uses logo"
        );
        Check(
            ItemVisuals.TryAttach(reward, info with { ItemName = "Unknown" }, _ => { }) == ApLogoCrushableVisual.Visual,
            "known unmapped item uses logo"
        );
        NativeModelCrushableVisual.Waiting = true;
        Check(ItemVisuals.TryAttach(reward, info, _ => { }) == null, "native resource pending retries");
        NativeModelCrushableVisual.Waiting = false;
        NativeModelCrushableVisual.Failed = true;
        Check(
            ItemVisuals.TryAttach(reward, info, _ => { }) == ApLogoCrushableVisual.Visual,
            "failed resource uses logo"
        );
        NativeModelCrushableVisual.Failed = false;
        Console.WriteLine("PASS: delayed scouting, native visual retry and AP logo fallback");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
