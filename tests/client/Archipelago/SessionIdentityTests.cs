using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UntilYouFallArchipelago;

internal static class SessionIdentityTests
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-session-identity-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var original = LegacyIdentity("wss://archipelago.gg:58880", "real-session", 0, 1);
            var originalPath = WriteState(directory, original, 196, 228);
            var originalBytes = File.ReadAllBytes(originalPath);
            foreach (
                var endpoint in new[]
                {
                    "ws://archipelago.gg:53869",
                    "wss://archipelago.gg:53869",
                    "ws://localhost:38281",
                }
            )
            {
                var restored = ApState.Open(directory, LegacyIdentity(endpoint, "real-session", 0, 1));
                Check(
                    restored.Identity == original
                        && restored.HandledCount == 196
                        && restored.NotifiedReceivedCount == 196
                        && restored.Checks.Count == 228
                        && restored.QuestProgress[1] == 12
                        && restored.GoalRuns == 3,
                    "changing transport, host or port must restore the complete original session, not replay its items"
                );
            }
            Check(
                File.ReadAllBytes(originalPath).SequenceEqual(originalBytes),
                "lookup must not rewrite the original save"
            );
            var stableIdentity = JsonSerializer.Serialize(
                new
                {
                    seed = "real-session",
                    Team = 0,
                    Slot = 1,
                }
            );
            var stable = ApState.Open(directory, stableIdentity);
            Check(
                stable.Identity == original,
                "endpoint-free login must retain the legacy identity used by starting traits"
            );
            stable.RecordCheck(999999);
            Check(
                ApState.Open(directory, original).Checks.Contains(999999) && Directory.GetFiles(directory).Length == 1,
                "future saves must continue in the same original file"
            );
            foreach (
                var identity in new[]
                {
                    LegacyIdentity("ws://elsewhere:1", "different-seed", 0, 1),
                    LegacyIdentity("ws://elsewhere:1", "real-session", 1, 1),
                    LegacyIdentity("ws://elsewhere:1", "real-session", 0, 2),
                }
            )
                Check(
                    ApState.Open(directory, identity).HandledCount == 0,
                    "different seeds, teams and slots must stay isolated"
                );

            var replayIdentity = LegacyIdentity("ws://archipelago.gg:53869", "real-session", 0, 1);
            var replayPath = WriteState(directory, replayIdentity, 33, 0);
            var beforeConflict = Directory.GetFiles(directory).ToDictionary(path => path, File.ReadAllBytes);
            var rejected = false;
            try
            {
                ApState.Open(directory, replayIdentity);
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }
            Check(
                rejected,
                "two divergent saves must stop login instead of preferring the exact endpoint or guessing a winner"
            );
            Check(
                beforeConflict.All(entry => File.ReadAllBytes(entry.Key).SequenceEqual(entry.Value)),
                "ambiguous lookup must not merge, overwrite or delete either save"
            );
            File.Delete(replayPath);

            var freshIdentity = JsonSerializer.Serialize(
                new
                {
                    seed = "fresh-session",
                    Team = 0,
                    Slot = 1,
                }
            );
            var fresh = ApState.Open(directory, freshIdentity);
            fresh.CommitItem(0);
            Check(
                ApState.Open(directory, LegacyIdentity("wss://other-host:99", "fresh-session", 0, 1)).HandledCount == 1,
                "new endpoint-free states must survive future address changes too"
            );
            Check(
                ApState.Open(directory, "opaque-test-identity").HandledCount == 0,
                "existing non-session identities remain supported"
            );
        }
        finally
        {
            Directory.Delete(directory, true);
        }
        Console.WriteLine(
            "PASS: session identity survives address changes, restores old progress and refuses conflicting saves"
        );
    }

    private static string LegacyIdentity(string endpoint, string seed, int team, int slot) =>
        JsonSerializer.Serialize(
            new
            {
                endpoint,
                seed,
                Team = team,
                Slot = slot,
            }
        );

    private static string WriteState(string directory, string identity, int handled, int checks)
    {
        var path = Path.Combine(
            directory,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))) + ".json"
        );
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                new
                {
                    Identity = identity,
                    HandledCount = handled,
                    NotifiedReceivedCount = handled,
                    Checks = Enumerable.Range(1, checks).Select(index => (long)index).ToArray(),
                    QuestProgress = new Dictionary<int, int> { [1] = 12 },
                    GoalRuns = 3,
                }
            )
        );
        return path;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
