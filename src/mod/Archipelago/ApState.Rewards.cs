using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UntilYouFallArchipelago;

internal sealed partial class ApState
{
    public void RecordReceivedNotice(int index)
    {
        if (index < NotifiedReceivedCount)
            return;
        if (index != NotifiedReceivedCount)
            throw new InvalidOperationException("AP received notifications must be recorded in order");

        NotifiedReceivedCount++;
        try
        {
            Save();
        }
        catch
        {
            NotifiedReceivedCount--;
            throw;
        }
    }

    public int? IncrementStageRewardPity(string room)
    {
        if (string.IsNullOrWhiteSpace(room))
            throw new ArgumentException("Missing room identity", nameof(room));
        if (room == LastStageRewardAttemptRoom)
            return null;

        var previous = (StageRewardPity, LastStageRewardAttemptRoom);
        StageRewardPity = Math.Min(StageRewardPity + 1, StageRewardProgress.MaximumAttempts);
        LastStageRewardAttemptRoom = room;
        try
        {
            Save();
        }
        catch
        {
            (StageRewardPity, LastStageRewardAttemptRoom) = previous;
            throw;
        }

        return StageRewardPity;
    }

    public void SetStageRewardOffer(string roomIdentity, long locationId, int slot)
    {
        if (string.IsNullOrWhiteSpace(roomIdentity))
            throw new ArgumentException("Missing room identity", nameof(roomIdentity));
        if (!StageRewardChecks.TryGetIndex(locationId, out _))
            throw new ArgumentOutOfRangeException(nameof(locationId));
        if (slot < 0)
            throw new ArgumentOutOfRangeException(nameof(slot));

        var previous = (StageRewardOfferRoom, StageRewardOfferLocationId, StageRewardOfferSlot);
        StageRewardOfferRoom = roomIdentity;
        StageRewardOfferLocationId = locationId;
        StageRewardOfferSlot = slot;
        try
        {
            Save();
        }
        catch
        {
            (StageRewardOfferRoom, StageRewardOfferLocationId, StageRewardOfferSlot) = previous;
            throw;
        }
    }

    public void ClearStageRewardOffer()
    {
        if (StageRewardOfferRoom == null)
            return;

        var previous = (StageRewardOfferRoom, StageRewardOfferLocationId, StageRewardOfferSlot);
        StageRewardOfferRoom = null;
        StageRewardOfferLocationId = null;
        StageRewardOfferSlot = null;
        try
        {
            Save();
        }
        catch
        {
            (StageRewardOfferRoom, StageRewardOfferLocationId, StageRewardOfferSlot) = previous;
            throw;
        }
    }

    public bool CompleteStageReward(long locationId)
    {
        if (!StageRewardChecks.TryGetIndex(locationId, out _))
            throw new ArgumentOutOfRangeException(nameof(locationId));
        if (Checks.Contains(locationId))
            return false;
        if (StageRewardOfferLocationId != locationId)
            throw new InvalidOperationException("Invalid location id");

        var previous = (
            StageRewardPity,
            StageRewardOfferRoom,
            StageRewardOfferLocationId,
            StageRewardOfferSlot,
            LastCompletedStageRewardRoom
        );
        Checks.Add(locationId);
        StageRewardPity = 0;
        LastCompletedStageRewardRoom = StageRewardOfferRoom;
        StageRewardOfferRoom = null;
        StageRewardOfferLocationId = null;
        StageRewardOfferSlot = null;
        try
        {
            Save();
        }
        catch
        {
            Checks.Remove(locationId);
            (
                StageRewardPity,
                StageRewardOfferRoom,
                StageRewardOfferLocationId,
                StageRewardOfferSlot,
                LastCompletedStageRewardRoom
            ) = previous;
            throw;
        }

        return true;
    }
}
