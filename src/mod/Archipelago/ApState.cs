using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UntilYouFall.Shared;

namespace UntilYouFallArchipelago;

internal sealed partial class ApState
{
    private readonly string _path;
    public string Identity { get; set; } = "";
    public int HandledCount { get; set; }
    public int NotifiedReceivedCount { get; private set; }
    public HashSet<long> Checks { get; set; } = [];
    public HashSet<long> WeaponUnlocks { get; private set; } = [];
    public HashSet<long> HandUnlocks { get; private set; } = [];
    public HashSet<long> TraitSlotUnlocks { get; private set; } = [];
    public HashSet<long> WeaponAbilityUnlocks { get; private set; } = [];
    public HashSet<long> DifficultyUnlocks { get; private set; } = [];
    public int WeaponUpgradeItemCount { get; private set; }
    public int AetherMultiplierItemCount { get; private set; }
    public Dictionary<long, int> PlayerBonusItemCounts { get; private set; } = [];
    public Dictionary<long, int> StartingTraitItemCounts { get; private set; } = [];
    public StartingTraitRun? StartingTraitRun { get; private set; }
    public StartingTraitOffer? StartingTraitOffer { get; private set; }
    public int StageRewardPity { get; private set; }
    public string? LastStageRewardAttemptRoom { get; private set; }
    public string? StageRewardOfferRoom { get; private set; }
    public long? StageRewardOfferLocationId { get; private set; }
    public int? StageRewardOfferSlot { get; private set; }
    public string? LastCompletedStageRewardRoom { get; private set; }
    public int LastEliteEncounterTrapIndex { get; private set; } = -1;
    public string? PendingEliteEncounterTrap { get; private set; }
    public HashSet<int> VoidedRunOnlyEffects { get; private set; } = [];
    public Dictionary<int, int> QuestProgress { get; private set; } = [];
    public HashSet<int> QuestHinted { get; private set; } = [];
    public HashSet<string> SupersUsed { get; private set; } = [];
    public QuestRunState QuestRun { get; private set; } = new();
    public int GoalRuns { get; private set; }
    public int GoalMaxDifficultyRuns { get; private set; }

    private sealed record SavedState(
        string Identity,
        int HandledCount,
        HashSet<long> Checks,
        int? NotifiedReceivedCount = null,
        HashSet<long>? WeaponUnlocks = null,
        HashSet<long>? HandUnlocks = null,
        HashSet<long>? TraitSlotUnlocks = null,
        HashSet<long>? WeaponAbilityUnlocks = null,
        int? WeaponUpgradeItemCount = null,
        int? AetherMultiplierItemCount = null,
        Dictionary<long, int>? PlayerBonusItemCounts = null,
        int? StageRewardPity = null,
        string? StageRewardOfferRoom = null,
        long? StageRewardOfferLocationId = null,
        int? StageRewardOfferSlot = null,
        string? LastCompletedStageRewardRoom = null,
        int? LastEliteEncounterTrapIndex = null,
        string? PendingEliteEncounterTrap = null,
        [property: JsonPropertyName("VoidedRunFillers")] HashSet<int>? VoidedRunOnlyEffects = null,
        HashSet<long>? DifficultyUnlocks = null,
        Dictionary<int, int>? QuestProgress = null,
        HashSet<int>? QuestHinted = null,
        QuestRunState? QuestRun = null,
        HashSet<string>? SupersUsed = null,
        int? GoalRuns = null,
        int? GoalMaxDifficultyRuns = null,
        string? LastStageRewardAttemptRoom = null,
        Dictionary<long, int>? StartingTraitItemCounts = null,
        StartingTraitRun? StartingTraitRun = null,
        StartingTraitOffer? StartingTraitOffer = null
    );

    private ApState(string path, string identity)
    {
        _path = path;
        Identity = identity;
    }

    public static ApState Open(string directory, string identity)
    {
        Directory.CreateDirectory(directory);
        string StatePath(string value) =>
            Path.Combine(directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))) + ".json");
        var session = SessionKey(identity);
        if (session != null)
        {
            string? matchingIdentity = null;
            foreach (var candidate in Directory.EnumerateFiles(directory, "*.json"))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(candidate));
                var savedIdentity =
                    document.RootElement.GetProperty("Identity").GetString()
                    ?? throw new InvalidDataException("Missing AP state identity");
                if (SessionKey(savedIdentity) != session)
                    continue;
                if (
                    !string.Equals(
                        Path.GetFileName(candidate),
                        Path.GetFileName(StatePath(savedIdentity)),
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    throw new InvalidDataException("AP state file name does not match its identity");
                if (matchingIdentity != null)
                    throw new InvalidDataException("Multiple AP saves match, resolve the conflict saves first");
                matchingIdentity = savedIdentity;
            }
            identity = matchingIdentity ?? identity;
        }
        var path = StatePath(identity);
        var state = new ApState(path, identity);
        if (File.Exists(path))
        {
            var saved =
                JsonSerializer.Deserialize<SavedState>(File.ReadAllText(path))
                ?? throw new InvalidDataException("Empty AP state file");
            if (saved.Identity != identity || saved.HandledCount < 0 || saved.Checks == null)
                throw new InvalidDataException("Invalid AP state identity or cursor");
            state.HandledCount = saved.HandledCount;
            state.NotifiedReceivedCount = saved.NotifiedReceivedCount ?? saved.HandledCount;
            if (state.NotifiedReceivedCount < 0)
                throw new InvalidDataException("Invalid AP notification cursor");
            state.Checks = saved.Checks;
            state.WeaponUnlocks = saved.WeaponUnlocks ?? [];
            state.DifficultyUnlocks = saved.DifficultyUnlocks ?? [];
            state.QuestProgress = saved.QuestProgress ?? [];
            state.QuestHinted = saved.QuestHinted ?? [];
            state.SupersUsed = saved.SupersUsed ?? [];
            state.QuestRun = saved.QuestRun ?? new();
            state.GoalRuns = saved.GoalRuns ?? 0;
            state.GoalMaxDifficultyRuns = saved.GoalMaxDifficultyRuns ?? 0;
            if (state.GoalRuns < 0 || state.GoalMaxDifficultyRuns < 0)
                throw new InvalidDataException("Invalid goal run counters");
            if (state.QuestRun.StartingAether < 0)
                throw new InvalidDataException("Invalid quest run starting aether");
            state.HandUnlocks = saved.HandUnlocks ?? [];
            state.TraitSlotUnlocks = saved.TraitSlotUnlocks ?? [];
            state.WeaponAbilityUnlocks = saved.WeaponAbilityUnlocks ?? [];
            state.WeaponUpgradeItemCount = saved.WeaponUpgradeItemCount ?? 0;
            if (state.WeaponUpgradeItemCount is < 0 or > WeaponUpgradeCapRules.MaximumItemCount)
                throw new InvalidDataException("Invalid progressive weapon upgrade count");
            state.AetherMultiplierItemCount = saved.AetherMultiplierItemCount ?? 0;
            if (state.AetherMultiplierItemCount is < 0 or > AetherMultiplierRules.MaxItemCount)
                throw new InvalidDataException("Invalid progressive aether multiplier count");
            state.PlayerBonusItemCounts = saved.PlayerBonusItemCounts ?? [];
            state.StartingTraitItemCounts = saved.StartingTraitItemCounts ?? [];
            state.StartingTraitRun = saved.StartingTraitRun;
            state.StartingTraitOffer = saved.StartingTraitOffer;
            if (
                state.StartingTraitItemCounts.Any(entry =>
                    entry.Value <= 0 || entry.Value > StartingTraitRules.MaxCount(entry.Key)
                )
                || state.StartingTraitRun != null && !StartingTraitRules.Valid(state.StartingTraitRun)
                || state.StartingTraitOffer != null && !StartingTraitRules.Valid(state.StartingTraitOffer)
            )
                throw new InvalidDataException("Invalid starting trait state");
            if (
                state.PlayerBonusItemCounts.Any(entry =>
                    !ApItems.IsPlayerBonus(entry.Key)
                    || entry.Value <= 0
                    || entry.Value > PlayerBonusRules.MaxCount(entry.Key)
                )
            )
                throw new InvalidDataException("Invalid player bonus item count");
            state.StageRewardPity = saved.StageRewardPity ?? 0;
            if (state.StageRewardPity is < 0 or > StageRewardProgress.MaximumAttempts)
                throw new InvalidDataException("Invalid Stage Reward pity count");
            state.StageRewardOfferRoom = saved.StageRewardOfferRoom;
            state.StageRewardOfferLocationId = saved.StageRewardOfferLocationId;
            state.StageRewardOfferSlot = saved.StageRewardOfferSlot;
            state.LastCompletedStageRewardRoom = saved.LastCompletedStageRewardRoom;
            state.LastStageRewardAttemptRoom =
                saved.LastStageRewardAttemptRoom ?? state.StageRewardOfferRoom ?? state.LastCompletedStageRewardRoom;
            if (state.LastStageRewardAttemptRoom != null && string.IsNullOrWhiteSpace(state.LastStageRewardAttemptRoom))
                throw new InvalidDataException("Invalid Stage Reward attempt room state");
            state.LastEliteEncounterTrapIndex = saved.LastEliteEncounterTrapIndex ?? -1;
            state.PendingEliteEncounterTrap = saved.PendingEliteEncounterTrap;
            state.VoidedRunOnlyEffects = saved.VoidedRunOnlyEffects ?? [];
            if (state.VoidedRunOnlyEffects.Any(index => index < 0))
                throw new InvalidDataException("Invalid voided run-only effect index");
            if (
                state.LastEliteEncounterTrapIndex < -1
                || (
                    state.PendingEliteEncounterTrap != null
                    && !Guid.TryParseExact(state.PendingEliteEncounterTrap, "N", out _)
                )
            )
                throw new InvalidDataException("Invalid Elite Encounter Trap state");
            var hasOffer =
                state.StageRewardOfferRoom != null
                || state.StageRewardOfferLocationId != null
                || state.StageRewardOfferSlot != null;
            if (
                hasOffer
                && (
                    string.IsNullOrWhiteSpace(state.StageRewardOfferRoom)
                    || state.StageRewardOfferLocationId is not long locationId
                    || !StageRewardChecks.TryGetIndex(locationId, out _)
                    || state.StageRewardOfferSlot is not int slot
                    || slot < 0
                )
            )
                throw new InvalidDataException("Invalid Stage Reward offer state");
            if (
                state.LastCompletedStageRewardRoom != null
                && string.IsNullOrWhiteSpace(state.LastCompletedStageRewardRoom)
            )
                throw new InvalidDataException("Invalid completed Stage Reward room state");
        }

        return state;
    }

    private static (string Seed, int Team, int Slot)? SessionKey(string identity)
    {
        try
        {
            using var document = JsonDocument.Parse(identity);
            var root = document.RootElement;
            if (
                root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("seed", out var seed)
                || seed.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(seed.GetString())
                || !root.TryGetProperty("Team", out var team)
                || team.ValueKind != JsonValueKind.Number
                || !team.TryGetInt32(out var teamNumber)
                || teamNumber < 0
                || !root.TryGetProperty("Slot", out var slot)
                || slot.ValueKind != JsonValueKind.Number
                || !slot.TryGetInt32(out var slotNumber)
                || slotNumber < 1
            )
                return null;

            return (seed.GetString()!, teamNumber, slotNumber);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void RecordCheck(long id)
    {
        if (!Checks.Add(id))
            return;

        try
        {
            Save();
        }
        catch
        {
            Checks.Remove(id);
            throw;
        }
    }

    public void MarkQuestHinted(int quest)
    {
        if (!QuestHinted.Add(quest))
            return;

        try
        {
            Save();
        }
        catch
        {
            QuestHinted.Remove(quest);
            throw;
        }
    }

    public void SetQuestProgress(int quest, int value)
    {
        QuestProgress.TryGetValue(quest, out var previous);
        QuestProgress[quest] = value;
        try
        {
            Save();
        }
        catch
        {
            QuestProgress[quest] = previous;
            throw;
        }
    }

    public bool MarkSuperUsed(string name)
    {
        if (!SupersUsed.Add(name))
            return false;

        try
        {
            Save();
        }
        catch
        {
            SupersUsed.Remove(name);
            throw;
        }
        return true;
    }

    public void RecordGoalRun(bool maxDifficulty)
    {
        GoalRuns++;
        if (maxDifficulty)
            GoalMaxDifficultyRuns++;
        try
        {
            Save();
        }
        catch
        {
            GoalRuns--;
            if (maxDifficulty)
                GoalMaxDifficultyRuns--;
            throw;
        }
    }

    public void SetQuestRun(QuestRunState run)
    {
        if (run == QuestRun)
            return;

        var previous = QuestRun;
        QuestRun = run;
        try
        {
            Save();
        }
        catch
        {
            QuestRun = previous;
            throw;
        }
    }

    public void CommitItem(int index)
    {
        if (index != HandledCount)
            throw new InvalidOperationException("AP items must be applied in order");

        var wasVoided = VoidedRunOnlyEffects.Remove(index);
        HandledCount++;
        try
        {
            Save();
        }
        catch
        {
            HandledCount--;
            if (wasVoided)
                VoidedRunOnlyEffects.Add(index);
            throw;
        }
    }

    public void VoidRunOnlyEffect(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (index < HandledCount || !VoidedRunOnlyEffects.Add(index))
            return;

        try
        {
            Save();
        }
        catch
        {
            VoidedRunOnlyEffects.Remove(index);
            throw;
        }
    }

    public bool RegisterEliteEncounterTrap(int index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (index < HandledCount || index <= LastEliteEncounterTrapIndex)
            return false;

        var previous = (LastEliteEncounterTrapIndex, PendingEliteEncounterTrap);
        LastEliteEncounterTrapIndex = index;
        PendingEliteEncounterTrap ??= Guid.NewGuid().ToString("N");
        try
        {
            Save();
        }
        catch
        {
            (LastEliteEncounterTrapIndex, PendingEliteEncounterTrap) = previous;
            throw;
        }

        return true;
    }

    public bool RegisterDeathLinkEliteTrap()
    {
        if (PendingEliteEncounterTrap != null)
            return false;

        PendingEliteEncounterTrap = Guid.NewGuid().ToString("N");
        try
        {
            Save();
        }
        catch
        {
            PendingEliteEncounterTrap = null;
            throw;
        }

        return true;
    }

    public bool CompleteEliteEncounterTrap(string receipt)
    {
        if (PendingEliteEncounterTrap != receipt)
            return false;

        PendingEliteEncounterTrap = null;
        try
        {
            Save();
        }
        catch
        {
            PendingEliteEncounterTrap = receipt;
            throw;
        }

        return true;
    }

    public void ResetItemHistory()
    {
        var snapshot = (
            HandledCount,
            NotifiedReceivedCount,
            WeaponUnlocks,
            HandUnlocks,
            TraitSlotUnlocks,
            WeaponAbilityUnlocks,
            WeaponUpgradeItemCount,
            AetherMultiplierItemCount,
            PlayerBonusItemCounts,
            LastEliteEncounterTrapIndex,
            PendingEliteEncounterTrap,
            VoidedRunOnlyEffects,
            DifficultyUnlocks,
            StartingTraitItemCounts
        );

        HandledCount = 0;
        NotifiedReceivedCount = 0;
        WeaponUnlocks = [];
        HandUnlocks = [];
        TraitSlotUnlocks = [];
        WeaponAbilityUnlocks = [];
        DifficultyUnlocks = [];
        WeaponUpgradeItemCount = 0;
        AetherMultiplierItemCount = 0;
        PlayerBonusItemCounts = [];
        StartingTraitItemCounts = [];
        LastEliteEncounterTrapIndex = -1;
        PendingEliteEncounterTrap = null;
        VoidedRunOnlyEffects = [];

        try
        {
            Save();
        }
        catch
        {
            (
                HandledCount,
                NotifiedReceivedCount,
                WeaponUnlocks,
                HandUnlocks,
                TraitSlotUnlocks,
                WeaponAbilityUnlocks,
                WeaponUpgradeItemCount,
                AetherMultiplierItemCount,
                PlayerBonusItemCounts,
                LastEliteEncounterTrapIndex,
                PendingEliteEncounterTrap,
                VoidedRunOnlyEffects,
                DifficultyUnlocks,
                StartingTraitItemCounts
            ) = snapshot;
            throw;
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(
            new SavedState(
                Identity,
                HandledCount,
                Checks,
                NotifiedReceivedCount,
                WeaponUnlocks,
                HandUnlocks,
                TraitSlotUnlocks,
                WeaponAbilityUnlocks,
                WeaponUpgradeItemCount,
                AetherMultiplierItemCount,
                PlayerBonusItemCounts,
                StageRewardPity,
                StageRewardOfferRoom,
                StageRewardOfferLocationId,
                StageRewardOfferSlot,
                LastCompletedStageRewardRoom,
                LastEliteEncounterTrapIndex,
                PendingEliteEncounterTrap,
                VoidedRunOnlyEffects,
                DifficultyUnlocks,
                QuestProgress,
                QuestHinted,
                QuestRun,
                SupersUsed,
                GoalRuns,
                GoalMaxDifficultyRuns,
                LastStageRewardAttemptRoom,
                StartingTraitItemCounts,
                StartingTraitRun,
                StartingTraitOffer
            )
        );

        AtomicFile.Write(_path, temporary => File.WriteAllText(temporary, json));
    }
}
