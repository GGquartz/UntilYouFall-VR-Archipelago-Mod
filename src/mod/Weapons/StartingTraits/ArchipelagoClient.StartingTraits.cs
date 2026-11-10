namespace UntilYouFallArchipelago;

internal sealed partial class ArchipelagoClient
{
    public string? StartingTraitIdentity => _state?.Identity;
    public IReadOnlyDictionary<long, int> StartingTraitItemCounts =>
        _state?.StartingTraitItemCounts ?? EmptyPlayerBonusCounts;

    public StartingTraitRun? PrepareStartingTraits(string key, bool newRun, IEnumerable<long> eligible, Random random)
    {
        if (_state == null)
            return null;
        if (_state.StartingTraitRun?.Key == key)
            return _state.StartingTraitRun;
        if (!newRun)
            return null;

        var offer = _state.StartingTraitOffer;
        if (offer == null && _state.StartingTraitRun != null)
            offer = StartingTraitRules.MigrateOffer(_state.StartingTraitRun, StartingTraitItemCounts);
        var reroll = offer == null || StartingTraitRules.NeedsReroll(offer, StartingTraitItemCounts);
        if (reroll)
            offer = StartingTraitRules.CreateOffer(StartingTraitItemCounts, random);
        var run = StartingTraitRules.Create(key, StartingTraitItemCounts, eligible, offer!);
        _state.SetStartingTraitRun(run, offer);

        _startingTraitsLog.Write(
            $"offer {(reroll ? "rolled" : "reused")} key={key} stages={offer!.ClearedRooms.Length}/3 options={run.Offered.Length} pickLimit={run.PickLimit}"
        );
        return run;
    }

    public bool RecordStartingTraitStageClear(string key, int stage)
    {
        var run = _state?.StartingTraitRun;
        if (run?.Key != key || stage < 1 || stage > StageChecks.Count)
            return false;
        var offer = _state!.StartingTraitOffer ?? StartingTraitRules.MigrateOffer(run, StartingTraitItemCounts);
        var room = $"{key}:{stage}";
        if (offer.ClearedRooms.Length >= StartingTraitRules.StagesPerReroll || offer.ClearedRooms.Contains(room))
            return false;

        offer = offer with { ClearedRooms = offer.ClearedRooms.Append(room).ToArray() };
        _state.SetStartingTraitRun(run, offer);

        _startingTraitsLog.Write($"stage credit room={room} stages={offer.ClearedRooms.Length}/3");
        return true;
    }

    public bool TakeStartingTrait(string key, long id)
    {
        var run = _state?.StartingTraitRun;
        if (run?.Key != key)
            return false;
        var picked = StartingTraitRules.Pick(run, id);
        if (ReferenceEquals(run, picked))
            return false;

        _state!.SetStartingTraitRun(picked);
        return true;
    }

    public bool SkipStartingTraits(string key)
    {
        var run = _state?.StartingTraitRun;
        if (run?.Key != key || run.Complete)
            return false;

        _state!.SetStartingTraitRun(StartingTraitRules.Skip(run));
        return true;
    }
}
