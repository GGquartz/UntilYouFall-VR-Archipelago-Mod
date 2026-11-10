using UntilYouFallArchipelago;

internal static class MultiNotificationTest
{
    public static async Task Run(string server)
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-ap-notice-multi-" + Guid.NewGuid());
        var oneNotices = new List<ApNotice>();
        var twoNotices = new List<ApNotice>();
        var one = new ArchipelagoClient(directory, Console.WriteLine, oneNotices.Add);
        var two = new ArchipelagoClient(directory, Console.WriteLine, twoNotices.Add);
        try
        {
            one.Connect(server, "UIPlayer1", "");
            two.Connect(server, "UIPlayer2", "");
            await Until(() => one.Connected && two.Connected);
            if (one.ConfirmedChecks != 0 || two.ConfirmedChecks != 0)
                throw new Exception("Use the fresh two-player notification test seed.");
            var starter = new[] { WeaponCatalog.ById(910024)! };
            for (var i = 1; i <= StageChecks.Count; i++)
            {
                one.ClearStage(i, starter);
                two.ClearStage(i, starter);
            }
            await Until(() =>
                one.ConfirmedChecks == StageChecks.Count
                && two.ConfirmedChecks == StageChecks.Count
                && one.PendingItems == StageChecks.Count
                && two.PendingItems == StageChecks.Count
            );
            await Task.Delay(500);
            one.Update();
            two.Update();
            var all = oneNotices.Concat(twoNotices).ToArray();
            var received = all.Where(n => n.PlainText.StartsWith("Received ")).ToArray();
            var sent = all.Where(n => n.PlainText.StartsWith("Sent ")).ToArray();
            var own = all.Count(n => n.PlainText == "Found your Shard of Vitality");
            if (
                received.Length == 0
                || received.Length != sent.Length
                || received.Length + own != 2 * StageChecks.Count
            )
                throw new Exception(
                    "Expected 42 receipts and matching remote sent notifications; seed must contain cross-player items."
                );
            if (
                !received.All(n =>
                    n.PlainText
                        is "Received Shard of Vitality from UIPlayer1"
                            or "Received Shard of Vitality from UIPlayer2"
                )
                || !sent.All(n =>
                    n.PlainText is "Sent Shard of Vitality to UIPlayer1" or "Sent Shard of Vitality to UIPlayer2"
                )
            )
                throw new Exception("Wrong item or player naming.");
            if (!all.All(n => n.RichText.Contains("<color=#00EEEE>Shard of Vitality</color>")))
                throw new Exception("Filler item color not preserved from server flags.");
            Console.WriteLine(
                $"PASS: two-player server notifications: received={received.Length}, sent={sent.Length}, own={own}"
            );
        }
        finally
        {
            one.Disconnect();
            two.Disconnect();
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

        async Task Until(Func<bool> condition)
        {
            var end = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < end)
            {
                one.Update();
                two.Update();
                if (condition())
                    return;
                await Task.Delay(25);
            }
            throw new Exception("Timed out waiting for two-player server.");
        }
    }
}
