namespace UntilYouFallArchipelago;

internal static class StageChecks
{
    public const int Count = LocationDefinitions.StageCount;
    public const string CampaignGuid = "02f47ab923a21894982a8c4c520f4842";

    public static bool TryGetStage(string? campaignGuid, int levelIndex, bool success, out int stage)
    {
        stage = 0;
        if (
            !success
            || !string.Equals(campaignGuid, CampaignGuid, StringComparison.OrdinalIgnoreCase)
            || levelIndex < 0
            || levelIndex >= Count
        )
            return false;

        stage = levelIndex + 1;
        return true;
    }

    public static long LocationId(int stage, long weaponId)
    {
        if (stage < 1 || stage > Count)
            throw new ArgumentOutOfRangeException(nameof(stage));
        var weaponOffset = Array.FindIndex(WeaponCatalog.All, weapon => weapon.Id == weaponId) + 1;
        if (weaponOffset < 1)
            throw new ArgumentOutOfRangeException(nameof(weaponId));

        return LocationDefinitions.StageWeaponBaseId + (stage - 1) * WeaponCatalog.All.Length + weaponOffset;
    }

    public static string StageName(int stage)
    {
        var suffix = LocationDefinitions.StageBossSuffixes.GetValueOrDefault(stage, "");

        return $"Clear Stage {stage}{suffix}";
    }

    public static string Name(int stage, string weaponName) => $"{StageName(stage)} with {weaponName}";

    public static IReadOnlyList<WeaponItem> ResolveWeapons(IEnumerable<string?> guids)
    {
        var result = new List<WeaponItem>();
        var seen = new HashSet<long>();
        foreach (var guid in guids)
        {
            if (string.IsNullOrWhiteSpace(guid))
                continue;
            var weapon = WeaponCatalog.ByGuid(guid);
            if (weapon != null && seen.Add(weapon.Id))
                result.Add(weapon);
        }

        return result;
    }
}
