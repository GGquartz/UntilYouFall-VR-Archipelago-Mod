namespace UntilYouFallArchipelago;

internal static class EliteEncounterTrapRules
{
    public const string CampaignGuid = StageChecks.CampaignGuid;
    public const string KnightRoomGuid = "192dfaee1ac3a954694e918dbcf95179";
    public const string KnightEncounterGuid = "02476d73c059dfa4bb2f5949c25a8016";
    public const string SpellweaverRoomGuid = "9890d71fa28039b45b6f14cce180a996";
    public const string SpellweaverEncounterGuid = "a591cce70bb3fec4c911fd9c9fff7781";
    public const string CaptainRoomGuid = "dce879e60c78372478100f21d3b0da72";
    public const string CaptainEncounterGuid = "e85aa7e3af9973842afe42db2553ef75";

    public static IReadOnlyList<(string RoomGuid, string EncounterGuid)> EliteEncounters { get; } =
        Array.AsReadOnly([
            (KnightRoomGuid, KnightEncounterGuid),
            (SpellweaverRoomGuid, SpellweaverEncounterGuid),
            (CaptainRoomGuid, CaptainEncounterGuid),
        ]);

    public const int KnightExcludedFromIndex = 13;

    public static bool CanSelectRoom(int index, string? roomGuid) =>
        index is >= 0 and < StageChecks.Count
        && EliteEncounters.Any(pair => pair.RoomGuid == roomGuid)
        && (index < KnightExcludedFromIndex || roomGuid != KnightRoomGuid);

    public static bool ShouldReplace(
        bool enabled,
        string? campaignGuid,
        int index,
        bool hasEncounter,
        bool isBoss,
        bool isLoading
    ) =>
        enabled
        && campaignGuid == CampaignGuid
        && index is >= 0 and < StageChecks.Count
        && hasEncounter
        && !isBoss
        && !isLoading;
}
