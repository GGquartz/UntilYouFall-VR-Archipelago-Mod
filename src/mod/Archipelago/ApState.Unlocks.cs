using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UntilYouFallArchipelago;

internal sealed partial class ApState
{
    public void UnlockWeapon(long id)
    {
        if (WeaponCatalog.ById(id) == null)
            throw new ArgumentOutOfRangeException(nameof(id));
        if (!WeaponUnlocks.Add(id))
            return;

        try
        {
            Save();
        }
        catch
        {
            WeaponUnlocks.Remove(id);
            throw;
        }
    }

    public void UnlockDifficulty(long id)
    {
        if (!DifficultySettings.IsItem(id))
            throw new ArgumentOutOfRangeException(nameof(id));
        if (!DifficultyUnlocks.Add(id))
            return;

        try
        {
            Save();
        }
        catch
        {
            DifficultyUnlocks.Remove(id);
            throw;
        }
    }

    public void UnlockHand(long id)
    {
        if (!ApItems.IsHand(id))
            throw new ArgumentOutOfRangeException(nameof(id));
        if (!HandUnlocks.Add(id))
            return;

        try
        {
            Save();
        }
        catch
        {
            HandUnlocks.Remove(id);
            throw;
        }
    }

    public void UnlockTraitSlot(long id)
    {
        if (!ApItems.IsTraitSlot(id))
            throw new ArgumentOutOfRangeException(nameof(id));
        if (!TraitSlotUnlocks.Add(id))
            return;

        try
        {
            Save();
        }
        catch
        {
            TraitSlotUnlocks.Remove(id);
            throw;
        }
    }

    public void UnlockWeaponAbility(long id)
    {
        if (!ApItems.IsWeaponAbility(id))
            throw new ArgumentOutOfRangeException(nameof(id));
        if (!WeaponAbilityUnlocks.Add(id))
            return;

        try
        {
            Save();
        }
        catch
        {
            WeaponAbilityUnlocks.Remove(id);
            throw;
        }
    }

    public void SetWeaponUpgradeItemCount(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        var normalized = Math.Min(count, WeaponUpgradeCapRules.MaximumItemCount);
        if (normalized <= WeaponUpgradeItemCount)
            return;

        var previous = WeaponUpgradeItemCount;
        WeaponUpgradeItemCount = normalized;
        try
        {
            Save();
        }
        catch
        {
            WeaponUpgradeItemCount = previous;
            throw;
        }
    }

    public void SetAetherMultiplierItemCount(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        var normalized = AetherMultiplierRules.Clamp(count);
        if (normalized <= AetherMultiplierItemCount)
            return;

        var previous = AetherMultiplierItemCount;
        AetherMultiplierItemCount = normalized;
        try
        {
            Save();
        }
        catch
        {
            AetherMultiplierItemCount = previous;
            throw;
        }
    }

    public int PlayerBonusItemCount(long id) => PlayerBonusItemCounts.GetValueOrDefault(id);

    public void SetStartingTraitItemCount(long id, int count)
    {
        var maximum = StartingTraitRules.MaxCount(id);
        if (maximum == 0 || count < 0)
            throw new ArgumentOutOfRangeException(nameof(id));

        var previous = StartingTraitItemCounts.GetValueOrDefault(id);
        var normalized = Math.Min(count, maximum);
        if (normalized <= previous)
            return;

        StartingTraitItemCounts[id] = normalized;
        try
        {
            Save();
        }
        catch
        {
            if (previous == 0)
                StartingTraitItemCounts.Remove(id);
            else
                StartingTraitItemCounts[id] = previous;
            throw;
        }
    }

    public void SetStartingTraitRun(StartingTraitRun run, StartingTraitOffer? offer = null)
    {
        if (!StartingTraitRules.Valid(run))
            throw new ArgumentException("Invalid starting trait offer", nameof(run));
        if (offer != null && !StartingTraitRules.Valid(offer))
            throw new ArgumentException("Invalid starting trait offer", nameof(offer));

        var previous = (StartingTraitRun, StartingTraitOffer);
        StartingTraitRun = run;
        if (offer != null)
            StartingTraitOffer = offer;
        try
        {
            Save();
        }
        catch
        {
            (StartingTraitRun, StartingTraitOffer) = previous;
            throw;
        }
    }

    public void SetPlayerBonusItemCount(long id, int count)
    {
        if (!ApItems.IsPlayerBonus(id))
            throw new ArgumentOutOfRangeException(nameof(id));
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        var normalized = PlayerBonusRules.Clamp(id, count);
        var previous = PlayerBonusItemCount(id);
        if (normalized <= previous)
            return;
        PlayerBonusItemCounts[id] = normalized;
        try
        {
            Save();
        }
        catch
        {
            if (previous == 0)
                PlayerBonusItemCounts.Remove(id);
            else
                PlayerBonusItemCounts[id] = previous;
            throw;
        }
    }
}
