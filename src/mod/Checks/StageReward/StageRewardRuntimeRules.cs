namespace UntilYouFallArchipelago;

internal static class StageRewardRuntimeRules
{
    public static bool CanOfferInRoom(string room, string? lastCompletedRoom) => room != lastCompletedRoom;

    public static bool ResourceIdentityComplete(string? name, string? developerName, string? guid) =>
        !string.IsNullOrWhiteSpace(name)
        && !string.IsNullOrWhiteSpace(developerName)
        && !string.IsNullOrWhiteSpace(guid);

    public static bool[] UniqueCandidateSlots(IReadOnlyList<bool> candidates, IReadOnlyList<int> instanceIds)
    {
        if (candidates.Count != instanceIds.Count)
            throw new ArgumentException("Slot lists must have equal length");

        return
        [
            .. Enumerable
                .Range(0, candidates.Count)
                .Select(index =>
                    candidates[index]
                    && instanceIds[index] != 0
                    && instanceIds.Count(id => id == instanceIds[index]) == 1
                ),
        ];
    }

    public static int? ReusableSlot(
        string room,
        long locationId,
        string? savedRoom,
        long? savedLocationId,
        int? savedSlot,
        IReadOnlyList<bool> aetherSlots,
        int deterministicRoll
    )
    {
        if (room != savedRoom || locationId != savedLocationId)
            return null;
        if (savedSlot is int slot && slot >= 0 && slot < aetherSlots.Count && aetherSlots[slot])
            return slot;

        return StageRewardOfferRules.SelectAetherSlot(aetherSlots, deterministicRoll);
    }

    public static int? SelectHistoryPlaceholder(IReadOnlyList<bool> aetherSlots, int replacedSlot)
    {
        for (var index = 0; index < aetherSlots.Count; index++)
            if (index != replacedSlot && !aetherSlots[index])
                return index;

        return null;
    }
}
