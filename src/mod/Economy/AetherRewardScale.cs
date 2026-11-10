using Il2CppSG.Claymore.Interaction;
using UnityEngine;

namespace UntilYouFallArchipelago;

internal static class AetherRewardScale
{
    public static void Apply(Action<string> log)
    {
        var scaled = 0;
        foreach (var augment in Resources.FindObjectsOfTypeAll<AetherMoteArmamentAugment>())
        {
            try
            {
                var vanilla = augment.aetherAmount;
                var next = AetherRewardScaleRules.ScaleFor(vanilla);
                if (next == null || next.Value == vanilla)
                    continue;

                augment.aetherAmount = next.Value;
                scaled++;
                log.Write("APRewardScale", $"Scaled {augment.name}: {vanilla} -> {next.Value}");
            }
            catch (Exception ex)
            {
                log.Write("APRewardScale", $"Scale failed for augment: {ex.Message}");
            }
        }
        log.Write("APRewardScale", $"Scaled {scaled} aether reward assets");
    }
}
