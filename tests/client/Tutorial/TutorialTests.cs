using UntilYouFallArchipelago;

internal static class TutorialTests
{
    public static void Run()
    {
        const string tutorial = "b16668e6a16418848ab46f22c8817b9c";
        Check(
            TutorialRules.ShouldReturnToRuneforge(true, false, tutorial, tutorial),
            "connected intro can return home"
        );
        Check(
            !TutorialRules.ShouldReturnToRuneforge(false, false, tutorial, tutorial),
            "offline or unsuccessful AP login cannot abort tutorial"
        );
        Check(
            !TutorialRules.ShouldReturnToRuneforge(true, true, tutorial, tutorial),
            "redirect waits for loading to finish"
        );
        Check(
            !TutorialRules.ShouldReturnToRuneforge(true, false, StageChecks.CampaignGuid, tutorial),
            "reconnect preserves an ordinary run"
        );
        Check(
            TutorialRules.ShouldReturnToRuneforge(
                true,
                false,
                StageChecks.CampaignGuid,
                tutorial,
                TutorialRules.ReactiveSupersRoomGuid
            ),
            "an old stalled Supers room returns to Runeforge"
        );
        Check(
            TutorialRules.ShouldReturnToRuneforge(
                true,
                false,
                StageChecks.CampaignGuid,
                tutorial,
                TutorialRules.ReactiveDodgingRoomGuid
            ),
            "a resumed Dodging room returns to Runeforge"
        );
        Check(
            !TutorialRules.ShouldReturnToRuneforge(
                false,
                false,
                StageChecks.CampaignGuid,
                tutorial,
                TutorialRules.ReactiveSupersRoomGuid
            )
                && !TutorialRules.ShouldReturnToRuneforge(
                    true,
                    true,
                    StageChecks.CampaignGuid,
                    tutorial,
                    TutorialRules.ReactiveSupersRoomGuid
                ),
            "reactive room recovery requires connection and finished loading"
        );
        Check(
            !TutorialRules.ShouldReturnToRuneforge(true, false, StageChecks.CampaignGuid, tutorial, "8744")
                && !TutorialRules.ShouldReturnToRuneforge(
                    true,
                    false,
                    "debug-campaign",
                    tutorial,
                    TutorialRules.ReactiveSupersRoomGuid
                ),
            "ordinary rooms and other campaigns are never aborted"
        );
        Check(
            TutorialRules.IsReactiveRoom(TutorialRules.ReactiveSupersRoomGuid)
                && TutorialRules.IsReactiveRoom("5E66EE94B96FC634D9ADE1049CBDAE2C"),
            "only the two real reactive room GUIDs can enter tutorial history"
        );
        foreach (
            var guid in new string?[]
            {
                null,
                "",
                TutorialRules.ReactiveSupersRoomGuid + "-extra",
                "941bc191029c23942a30e8f1668b8614",
                "unknown",
            }
        )
            Check(!TutorialRules.IsReactiveRoom(guid), "hub and other room histories are preserved: " + guid);
        Check(
            !TutorialRules.ShouldReturnToRuneforge(true, false, null, null)
                && !TutorialRules.ShouldReturnToRuneforge(true, false, "", ""),
            "missing campaigns cannot accidentally match"
        );
        Check(
            TutorialRules.IsTutorialPopup("PopupUIAsset_Tutorial_Hub_03_UpgradePortal")
                && TutorialRules.IsTutorialPopup("PopupUIAsset_ReactiveTutorial_03_Dodging"),
            "verified intro and reactive assets are filtered"
        );
        Check(
            TutorialRules.IsBattleTip("PopupUI_BattleTip_Cashout_Tier1")
                && TutorialRules.IsBattleTip("PopupUI_BattleTip_Cashout_Boss3")
                && TutorialRules.IsBattleTip("PopupUIAsset_BattleTip_Supers"),
            "battle tip display objects and their popup assets are filtered"
        );
        foreach (var name in new string?[] { null, "", "PopupUI_BattleTip", "PopupUI_Reward", "Other_BattleTip" })
            Check(!TutorialRules.IsBattleTip(name), "unrelated popup objects are preserved: " + name);
        foreach (
            var name in new string?[] { null, "", "PopupUIAsset_Error", "PopupUIAsset_Reward", "Other_Tutorial_Notice" }
        )
            Check(!TutorialRules.IsTutorialPopup(name), "ordinary popups are preserved: " + name);
        Console.WriteLine("PASS: tutorial room selection, reactive recovery guards and targeted popup filtering");
    }

    private static void Check(bool condition, string description)
    {
        if (!condition)
            throw new Exception("Tutorial skip: " + description);
    }
}
