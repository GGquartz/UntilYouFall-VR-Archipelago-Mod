using UntilYouFallArchipelago;

if (args.Length > 0)
{
    if (args[0] == "weapon-hints")
    {
        WeaponHintTests.Run();
        return;
    }
    if (args.Length > 1 && args[1] == "weapon-hints")
    {
        await WeaponHintTests.RunNetwork(args[0], args.Length > 2 && args[2] == "free");
        return;
    }
    if (args.Length > 1 && args[1] == "sync")
    {
        await ReconnectIntegrationTest.Run(args[0], args.Length > 2 ? args[2] : "UYFTest");
        return;
    }
    if (args.Length > 1 && args[1] == "console")
    {
        await WristConsoleIntegrationTest.Run(args[0]);
        return;
    }
    if (args.Length > 1 && args[1] == "multi")
    {
        await MultiNotificationTest.Run(args[0]);
        return;
    }
    await IntegrationTest.Run(args[0]);
    return;
}

SharedCatalogTests.Run();
RuneforgeShopLayoutTests.Run();
ApConsoleTests.Run();
TutorialTests.Run();
EnemyChecksTests.Run();
QuestChecksTests.Run();
GoalRulesTests.Run();
ClientProgressTests.Run();
StageProgressTests.Run();
WeaponHintTests.Run();
StartingWeaponTests.Run();
StartingHandsTests.Run();
DifficultyTests.Run();
HandUnlockTests.Run();
ExtraTraitSlotTests.Run();
StartingTraitTests.Run();
StartingTraitRerollTests.Run();
WeaponAbilityItemTests.Run();
PlayerBonusTests.Run();
WeaponUpgradeCapTests.Run();
DebugAetherTests.Run();
WorldTextRulesTests.Run();
ClosedCaptionFixTests.Run();
EnemySpawnTrapRulesTests.Run();
NoBlockingTrapStateTests.Run();
TimedTrapTests.Run();
DeathLinkControllerTests.Run();
CursedTrapStateTests.Run();
EnemyStasisStateTests.Run();
RunOnlyEffectTests.Run();
ReconnectStateTests.Run();
ClientConnectionTests.Run();
ClientSlotSettingsTests.Run();
ReceivedItemHistoryTests.Run();
SessionIdentityTests.Run();
ItemVisualModelsTests.Run();
ItemVisualsTests.Run();
AetherPurchaseTests.Run();
EliteEncounterTrapRulesTests.Run();
EnemySpawnTrapItemTests.Run();
EliteEncounterTrapItemTests.Run();
EliteEncounterOverrideTests.Run();
ApNotificationUiTests.Run();
AetherEconomyTests.Run();
TechnicalNoticeGateTests.Run();
WeaponAnalyticsTests.Run();
WeaponUiTests.Run();
SingleWeaponRewardTests.Run();
ProfileModStatusTests.Run();
RuneforgeShopRulesTests.Run();
ApRewardInspectionPanelTests.Run();
ApLogoMeshDataTests.Run();
ApLogoVisualRulesTests.Run();
StageWeaponChecksTests.Run();
StageDifficultyChecksTests.Run();
StageRewardChecksTests.Run();
StageRewardProgressTests.Run();
StageRewardOfferRulesTests.Run();
StageRewardPacingTests.Run();
StageRewardRuntimeRulesTests.Run();
StageRewardInspectionTests.Run();
StageRewardDiagnosticGuardTests.Run();
ApStateTests.Run();
StageCheckMappingTests.Run();
ApNoticeTests.Run();
