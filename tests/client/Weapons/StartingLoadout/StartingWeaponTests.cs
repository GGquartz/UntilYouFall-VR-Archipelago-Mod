using UntilYouFallArchipelago;

internal static class StartingWeaponTests
{
    public static void Run()
    {
        var slot = new Dictionary<string, object> { ["starting_weapons"] = new[] { "Cold Iron Greataxe" } };
        Assert(StartingWeaponRules.Read(slot).SequenceEqual([910017]), "slot-selected two-handed starter is accepted");
        slot["starting_weapons"] = new[] { "Mistcutter", "Ritual Dagger" };
        var starters = StartingWeaponRules.Read(slot);
        Assert(starters.SequenceEqual([910019, 910014]), "two distinct starters retain their order");
        foreach (var rightDominant in new[] { false, true })
        {
            Assert(
                StartingWeaponRules.ForHand(starters, StartingHands.Both, !rightDominant, rightDominant) == starters[0],
                "first starter is assigned to dominant hand"
            );
            Assert(
                StartingWeaponRules.ForHand(starters, StartingHands.Both, rightDominant, rightDominant) == starters[1],
                "second starter is assigned to other hand"
            );
            Assert(
                StartingWeaponRules.ForHand(new[] { starters[0] }, StartingHands.Both, rightDominant, rightDominant)
                    == null,
                "none retains an empty non-dominant hand"
            );
            foreach (var hands in new[] { StartingHands.LeftOnly, StartingHands.RightOnly })
            {
                var left = hands == StartingHands.LeftOnly;
                Assert(
                    StartingWeaponRules.ForHand(starters, hands, left, rightDominant) == starters[0]
                        && StartingWeaponRules.ForHand(starters, hands, !left, rightDominant) == null,
                    "single-hand starts equip only first starter without unlocking another hand"
                );
            }
        }
        foreach (
            var names in new[]
            {
                Array.Empty<string>(),
                ["Mistcutter", "Mistcutter"],
                ["Mistcutter", "Ritual Dagger", "Heartsplitter"],
                ["Mistcutter", "Unknown"],
                ["Mistcutter", "Spellsword's Charm"],
                ["Spellsword's Charm"],
                ["Knight-Blooded Crest"],
                ["Unknown"],
            }
        )
        {
            slot["starting_weapons"] = names;
            try
            {
                StartingWeaponRules.Read(slot);
                throw new Exception("invalid starter accepted");
            }
            catch (InvalidDataException) { }
        }
        try
        {
            StartingWeaponRules.Read(new Dictionary<string, object>());
            throw new Exception("missing starter accepted");
        }
        catch (InvalidDataException) { }
        Console.WriteLine("PASS: ordered one/two starters, hand assignment and invalid/duplicate starter rejection");
    }

    private static void Assert(bool ok, string message)
    {
        if (!ok)
            throw new Exception("Starting weapon: " + message);
    }
}
