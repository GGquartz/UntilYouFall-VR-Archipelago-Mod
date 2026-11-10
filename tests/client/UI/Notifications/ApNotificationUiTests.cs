using Archipelago.MultiClient.Net.Enums;
using UntilYouFallArchipelago;

internal static class ApNotificationUiTests
{
    public static void Run()
    {
        using var ui = new ApNotificationUi(_ => { });
        var notice = ApNotice.Plain("Spawn Failed - In Runeforge");
        ui.ShowTechnical(notice);
        ui.Update();
        var panel = VrNotificationPanel.Created.Last(panel => panel.Name == "Archipelago Technical Notification");
        Assert(panel.ShowCount == 1 && panel.LastNotice == notice, "the initial technical notice is presented");

        panel.IsReady = false;
        ui.Update();
        ui.ShowTechnical(ApNotice.Plain("Duplicate"));
        Thread.Sleep(550);
        ui.Update();
        Assert(
            panel.ShowCount == 2 && panel.LastNotice == notice,
            "renderer loss recreates the panel and retries the same notice without accepting duplicates"
        );

        Thread.Sleep(7100);
        ui.Update();
        Assert(!panel.Visible, "the retried notice hides after a full display interval");
        var next = ApNotice.Plain("Next");
        ui.ShowTechnical(next);
        Thread.Sleep(550);
        ui.Update();
        Assert(
            panel.ShowCount == 3 && panel.LastNotice == next,
            "completion releases the technical slot for a new message"
        );
        Console.WriteLine(
            "PASS: technical notification survives renderer loss, keeps deduplication and releases after retry"
        );
        RunKeepsRemainingTime();
        RunUrgentNoticesJumpTheQueue();
    }

    private static void RunKeepsRemainingTime()
    {
        using var ui = new ApNotificationUi(_ => { });
        ui.ShowTechnical(ApNotice.Plain("Room change"));
        ui.Update();
        var panel = VrNotificationPanel.Created.Last(panel => panel.Name == "Archipelago Technical Notification");

        Thread.Sleep(3500);
        panel.IsReady = false;
        ui.Update();
        Thread.Sleep(550);
        ui.Update();
        Assert(panel.ShowCount == 2 && panel.Visible, "the notice is shown again after the renderer is recreated");

        Thread.Sleep(3100);
        ui.Update();
        Assert(panel.Visible, "the re-shown notice is still visible within its remaining time");
        Thread.Sleep(600);
        ui.Update();
        Assert(!panel.Visible, "the re-shown notice hides after only the remaining time, not a fresh interval");
        Console.WriteLine("PASS: a re-shown notification keeps its remaining display time");
    }

    private static void RunUrgentNoticesJumpTheQueue()
    {
        var normal = ApNotice.Plain("Normal");
        var trap = ApNotice.Received("Cursed Trap", ItemFlags.Trap, "Player2");
        var death = ApNotice.DeathLink("Player2", "Player2 died");

        using (var sameBatch = new ApNotificationUi(_ => { }))
        {
            sameBatch.Enqueue(normal);
            sameBatch.Enqueue(trap);
            sameBatch.Update();
            var batchPanel = VrNotificationPanel.Created.Last(panel => panel.Name == "Archipelago Notification");
            Assert(batchPanel.LastNotice == trap, "a trap arriving together with a normal notice is shown first");
        }

        using var ui = new ApNotificationUi(_ => { });
        ui.Enqueue(normal);
        ui.Update();
        var panel = VrNotificationPanel.Created.Last(panel => panel.Name == "Archipelago Notification");
        Assert(panel.LastNotice == normal, "a normal notice is shown when nothing else is queued");

        ui.Enqueue(trap);
        ui.Enqueue(death);
        ui.Update();
        Assert(panel.LastNotice == trap && panel.Visible, "a trap replaces the normal notice on screen at once");

        Thread.Sleep(7100);
        ui.Update();
        Thread.Sleep(550);
        ui.Update();
        Assert(panel.LastNotice == death, "urgent notices keep their arrival order");

        Thread.Sleep(7100);
        ui.Update();
        Thread.Sleep(550);
        ui.Update();
        Assert(panel.LastNotice == normal && panel.Visible, "the normal notice returns after the urgent ones finish");
        Console.WriteLine("PASS: traps and DeathLink jump the notification queue and the displaced notice resumes");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception("Technical notification lifecycle: " + message);
    }
}

namespace UntilYouFallArchipelago
{
    internal sealed class VrNotificationPanel : IDisposable
    {
        public static readonly List<VrNotificationPanel> Created = [];
        public string Name { get; }
        public bool IsReady { get; set; }
        public bool Visible { get; private set; }
        public int ShowCount { get; private set; }
        public ApNotice? LastNotice { get; private set; }

        public VrNotificationPanel(Action<string> log, string name, string logPrefix, float verticalPosition)
        {
            Name = name;
            Created.Add(this);
        }

        public bool TryCreate() => IsReady = true;

        public void Show(ApNotice notice)
        {
            if (!IsReady)
                throw new InvalidOperationException("Renderer is unavailable.");
            ShowCount++;
            LastNotice = notice;
            Visible = true;
        }

        public void Hide() => Visible = false;

        public void LogRenderState() { }

        public void Dispose()
        {
            IsReady = false;
            Visible = false;
        }
    }
}
