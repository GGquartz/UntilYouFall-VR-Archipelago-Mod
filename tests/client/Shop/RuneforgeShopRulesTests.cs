using UntilYouFallArchipelago;

internal static class RuneforgeShopRulesTests
{
    public static void Run()
    {
        Assert(
            !RuneforgeShopRules.IsPreviewLocked(1, 0)
                && Enumerable.Range(2, 4).All(section => RuneforgeShopRules.IsPreviewLocked(section, 0))
                && !RuneforgeShopRules.IsPreviewLocked(3, 2)
                && RuneforgeShopRules.IsPreviewLocked(4, 2),
            "shop 1 is open; shop N needs N-1 Progressive Shop Unlock copies"
        );
        Assert(
            ShopChecks.Name(5, 6) == "Shop Item 5-6"
                && ShopChecks.LocationId(1, 1) == 911601
                && ShopChecks.LocationId(5, 6) == 911630
                && ShopChecks.AllIds().Length == 30
                && Enumerable.Range(1, 5).Select(ShopChecks.Price).SequenceEqual(new[] { 500, 1000, 1500, 2000, 2500 }),
            "shop checks match the APWorld contract"
        );
        const string sprite = "<sprite=\"StatIcons_SpriteAtlas\" index=6 tint=1>";
        Assert(
            RuneforgeShopRules.PriceText(sprite + "1740", 100) == sprite + "100"
                && RuneforgeShopRules.PriceText(null, 100) == sprite + "100",
            "price preserves the native aether sprite instead of the live currency balance"
        );
        Assert(
            RuneforgeShopRules.InRuneforge(RuneforgeShopRules.RuneforgeRoomGuid, false, false),
            "the shop is enabled in a fully loaded Runeforge"
        );
        Assert(
            !RuneforgeShopRules.InRuneforge("combat-room", false, false)
                && !RuneforgeShopRules.InRuneforge(RuneforgeShopRules.RuneforgeRoomGuid, true, false)
                && !RuneforgeShopRules.InRuneforge(RuneforgeShopRules.RuneforgeRoomGuid, false, true),
            "combat rooms and loading states cannot show the shop"
        );
        Assert(
            RuneforgeShopRules.PrefabResource == "Crushables/Rewards/Reward_Aether_Lvl01_HQ",
            "shop items use the verified native reward resource"
        );
        Assert(
            Math.Abs(RuneforgeShopRules.CrushSeconds - 2.5f) < 0.0001f,
            "AP shop items match the longest native reward crush time"
        );
        Assert(
            RuneforgeShopRules.VisualRetryDelayMs == 16,
            "an unavailable reward visual is retried on the next frame instead of seconds later"
        );
        Assert(
            RuneforgeShopRules.InspectionPanelWidth == 650f
                && RuneforgeShopRules.InspectionPanelHeight == 150f
                && RuneforgeShopRules.InspectionPanelOffsetY == 0f,
            "AP reward details keep the wide panel but use a compact two-line height"
        );
        Assert(
            RuneforgeShopRules.ShouldAttachVisual(true, false, true)
                && !RuneforgeShopRules.ShouldAttachVisual(false, false, true)
                && !RuneforgeShopRules.ShouldAttachVisual(true, true, true)
                && !RuneforgeShopRules.ShouldAttachVisual(true, false, false),
            "the custom visual waits for a spawned reward and its retry deadline"
        );
        Console.WriteLine("PASS: Runeforge shop scene gate and native resource");
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok)
            throw new Exception("Runeforge shop rules: " + message);
    }
}
