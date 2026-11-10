using System.Reflection;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.Models;
using UntilYouFallArchipelago;

internal static class DifficultyTests
{
    public static void Run()
    {
        for (var starting = 0; starting < 4; starting++)
        for (var maximum = starting; maximum < 4; maximum++)
        {
            var settings = DifficultySettings.Read(
                new Dictionary<string, object>
                {
                    ["starting_difficulty"] = (long)starting,
                    ["max_difficulty"] = (long)maximum,
                }
            );
            var owned = new HashSet<long> { DifficultySettings.ItemId(starting) };
            for (var tier = -1; tier <= 5; tier++)
                Assert(settings.Allows(tier, owned) == (tier == starting), "only starter is initially unlocked");
            owned.Add(DifficultySettings.ItemId(maximum));
            Assert(settings.Allows(maximum, owned), "out of order unlock is allowed");
            Assert(!settings.Allows(4, owned), "unreleased tiers stay locked");
            owned.Add(DifficultySettings.ItemId(0));
            Assert(
                settings.Allows(0, owned),
                "lower than starting unlocks independently, including starting equals max"
            );
        }
        foreach (var raw in new object[] { -1L, 4L, 1.5, true, "1", double.NaN })
        {
            var rejected = false;
            try
            {
                DifficultySettings.Read(
                    new Dictionary<string, object> { ["starting_difficulty"] = raw, ["max_difficulty"] = 3L }
                );
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }
            Assert(rejected, "invalid slot tier is rejected");
        }
        foreach (
            var slot in new[]
            {
                [],
                new Dictionary<string, object> { ["starting_difficulty"] = 2L, ["max_difficulty"] = 0L },
            }
        )
        {
            var rejected = false;
            try
            {
                DifficultySettings.Read(slot);
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }
            Assert(rejected, "missing or reversed range is rejected");
        }
        var directory = Path.Combine(Path.GetTempPath(), "uyf-difficulty-" + Guid.NewGuid());
        try
        {
            var state = ApState.Open(directory, "difficulty-seed");
            state.UnlockDifficulty(910463);
            state.UnlockDifficulty(910463);
            Assert(
                ApState.Open(directory, "difficulty-seed").DifficultyUnlocks.SetEquals(new[] { 910463L }),
                "unlock is durable and idempotent before the consumable cursor advances"
            );
            Assert(ApState.Open(directory, "other-seed").DifficultyUnlocks.Count == 0, "seed isolation");
            state.ResetItemHistory();
            Assert(
                ApState.Open(directory, "difficulty-seed").DifficultyUnlocks.Count == 0,
                "server reset clears unlocks"
            );
        }
        finally
        {
            Directory.Delete(directory, true);
        }
        CheckDispatch();
        CheckSilentStarter();
        Console.WriteLine("PASS: difficulty range, independent ownership, slot validation and durable unlocks");
    }

    private static void CheckDispatch()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-difficulty-dispatch-" + Guid.NewGuid());
        var state = ApState.Open(directory, "difficulty-dispatch");
        var client = new ArchipelagoClient(directory, _ => { });
        var session = ArchipelagoSessionFactory.CreateSession("ws://localhost:1");
        typeof(ArchipelagoSession)
            .GetField("<Socket>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(session, DispatchProxy.Create<IArchipelagoSocketHelper, EnemyTrapSocketProxy>());
        ClientSessionTestHelpers.AttachConnection(client, session);
        ClientSessionTestHelpers.AttachState(client, state);
        ClientSessionTestHelpers.SetLoggedIn(client, true);
        ClientSessionTestHelpers.SetSettings(client, "Difficulty", new DifficultySettings(1, 2));
        var pending =
            (SortedDictionary<int, long>)
                typeof(ArchipelagoClient)
                    .GetField("_pendingItems", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(client)!;
        pending[0] = 910460;
        pending[1] = 910463;
        void Unexpected() => throw new Exception("difficulty unlock invoked gameplay effect");
        try
        {
            client.ApplyNextItem(
                new ItemEffects
                {
                    CanHeal = () => false,
                    Heal = _ => Unexpected(),
                    ApplyAetherFiller = (_, _) => throw new Exception(),
                    CanSpawnEnemy = () => false,
                    SpawnEnemy = Unexpected,
                }
            );
            Assert(
                client.HandledCount == 1 && state.DifficultyUnlocks.SetEquals(new[] { 910460L }),
                "out of order tier unlock consumes without changing gameplay or unlocking intermediate tiers"
            );
            client.ApplyNextItem(
                new ItemEffects
                {
                    CanHeal = () => false,
                    Heal = _ => Unexpected(),
                    ApplyAetherFiller = (_, _) => throw new Exception(),
                    CanSpawnEnemy = () => false,
                    SpawnEnemy = Unexpected,
                }
            );
            Assert(
                client.HandledCount == 2 && state.DifficultyUnlocks.SetEquals(new[] { 910460L }),
                "out of range item is consumed without unlocking"
            );
            Assert(
                ApState.Open(directory, "difficulty-dispatch").DifficultyUnlocks.SetEquals(new[] { 910460L }),
                "dispatch ownership survives reconnect"
            );
        }
        finally
        {
            client.Disconnect();
            Directory.Delete(directory, true);
        }
    }

    private static void CheckSilentStarter()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-difficulty-notice-" + Guid.NewGuid());
        var state = ApState.Open(directory, "difficulty-notice");
        var notices = new List<ApNotice>();
        var client = new ArchipelagoClient(directory, _ => { }, notices.Add);
        var session = ArchipelagoSessionFactory.CreateSession("ws://localhost:1");
        typeof(ArchipelagoSession)
            .GetField("<Socket>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(session, DispatchProxy.Create<IArchipelagoSocketHelper, EnemyTrapSocketProxy>());
        ClientSessionTestHelpers.AttachConnection(client, session);
        ClientSessionTestHelpers.AttachState(client, state);
        ClientSessionTestHelpers.SetLoggedIn(client, true);
        var notifications = (ClientNotifications)ClientSessionTestHelpers.GetField(client, "_notifications");
        notifications.QueueReceived(
            0,
            new NetworkItem
            {
                Item = 910461,
                Location = -2,
                Player = 0,
            }
        );
        try
        {
            client.Update();
            Assert(
                notices.Count == 0 && state.NotifiedReceivedCount == 1,
                "starting difficulty is silent but its notification cursor advances"
            );
            Assert(
                ApState.Open(directory, "difficulty-notice").NotifiedReceivedCount == 1,
                "silent starter stays consumed after reconnect"
            );
            client.Update();
            Assert(notices.Count == 0, "starting difficulty does not announce on replay");
        }
        finally
        {
            client.Disconnect();
            Directory.Delete(directory, true);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
