using System.Reflection;
using Archipelago.MultiClient.Net;
using UntilYouFallArchipelago;

internal static class WeaponHintTests
{
    public static void Run()
    {
        foreach (
            var text in new[]
            {
                "You can't afford the hint. You have 0 points and need at least 4.",
                "Hint was previously used, no points deducted.",
                "Nothing found for recognized item name \"Weapons\".",
                "There may be more hintables, however, you cannot afford to pay for any more.",
            }
        )
            Check(ArchipelagoClient.IsWeaponHintStatus(text), "relevant server status: " + text);
        foreach (
            var text in new[]
            {
                "There may be more hintables, you can rerun the command to find more.",
                "A hint costs 4 points. You have 0 points.",
                "Player2: hint please",
            }
        )
            Check(!ArchipelagoClient.IsWeaponHintStatus(text), "ignore informational or unrelated messages: " + text);
        Console.WriteLine("PASS: Weapons group hint status filtering");
    }

    public static async Task RunNetwork(string server, bool free)
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-weapon-hint-test-" + Guid.NewGuid());
        var notices = new List<ApNotice>();
        var client = new ArchipelagoClient(directory, Console.WriteLine, notices.Add);
        try
        {
            client.Connect(server, "UYFTest", "");
            await Until(() => client.Connected);
            await Pump(500);
            Check(client.WeaponHintsEnabled, "Weapons group enables weapon hints without custom order data");
            var session = ClientSessionTestHelpers.Connection(client).Session!;
            var initial = WeaponCatalog.All.First(weapon => !client.StartingWeaponIds.Contains(weapon.Id)).Id;

            if (!free)
            {
                notices.Clear();
                Check(client.HintNextWeapon(), "no-points request started");
                await Until(() => client.CanHintWeapon);
                await Pump(400);
                Check(
                    notices.Count == 1 && notices[0].PlainText.StartsWith("You can't afford the hint."),
                    "exactly one insufficient-points notice"
                );
                Check((await session.DataStorage.GetHintsAsync()).Length == 0, "failed request creates no hint");
                client.FirstKill(910301);
                await Until(() => client.ConfirmedChecks == 1);
                await Pump(500);
            }

            session.Say($"!hint {initial}");
            await Pump(500);
            var before = await session.DataStorage.GetHintsAsync();
            Check(before.Any(hint => hint.ItemId == initial), "pre-existing hint installed");
            for (var request = 1; request <= 2; request++)
            {
                notices.Clear();
                Check(client.HintNextWeapon(), "weapon request started");
                Check(!client.HintNextWeapon(), "double crush does not send another command");
                await Until(() => client.CanHintWeapon);
                await Pump(500);
                var after = await session.DataStorage.GetHintsAsync();
                var added = after.Where(hint => !before.Any(old => old.ItemId == hint.ItemId)).ToArray();
                if (!free)
                    Check(added.Count(hint => !hint.Found) == 1, "paid group request buys exactly one unfound weapon");
                else if (request == 1)
                    Check(added.Length > 1, "free group request may reveal every remaining weapon");
                else
                    Check(added.Length == 0, "repeated free hint creates no duplicates");
                Check(notices.Count == 1, "group hint produces only one notification");
                if (added.Any(hint => !hint.Found))
                    Check(
                        added
                            .Where(hint => !hint.Found)
                            .Any(hint => notices[0].PlainText.Contains(WeaponCatalog.ById(hint.ItemId)!.Name)),
                        "notification shows a newly hinted weapon, not the pre-existing hint"
                    );
                else
                    Check(
                        notices[0].PlainText.StartsWith("Hint was previously used"),
                        "all known weapons show the relevant server status"
                    );
                before = after;

                if (request == 1)
                {
                    client.Disconnect();
                    client.Connect(server, "UYFTest", "");
                    await Until(() => client.Connected);
                    await Pump(500);
                    session = ClientSessionTestHelpers.Connection(client).Session!;
                }
            }
            Console.WriteLine(
                $"PASS: real AP server, {(free ? "free" : "paid")} Weapons group hints; "
                    + "one relevant notification, old hint exclusion, double-crush guard and reconnect"
            );
        }
        finally
        {
            client.Disconnect();
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

        async Task Until(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < deadline)
            {
                client.Update();
                if (condition())
                    return;
                await Task.Delay(25);
            }
            throw new Exception("Timed out waiting for a weapon hint response.");
        }

        async Task Pump(int milliseconds)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
            while (DateTime.UtcNow < deadline)
            {
                client.Update();
                await Task.Delay(25);
            }
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception("Weapon hints: " + message);
    }
}
