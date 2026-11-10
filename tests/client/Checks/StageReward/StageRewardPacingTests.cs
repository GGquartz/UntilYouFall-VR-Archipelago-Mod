using System.Reflection;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using UntilYouFallArchipelago;

internal static class StageRewardPacingTests
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-stage-reward-pacing-" + Guid.NewGuid());
        const string identity = "server/pacing-client/team0/slot1";
        var state = ApState.Open(directory, identity);
        foreach (var index in Enumerable.Range(1, 3))
            state.RecordCheck(StageRewardChecks.LocationId(index));
        var logs = new List<string>();
        var session = ArchipelagoSessionFactory.CreateSession("ws://localhost:1");
        typeof(ArchipelagoSession)
            .GetField("<Socket>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(session, DispatchProxy.Create<IArchipelagoSocketHelper, StageRewardSocketProxy>());
        var client = new ArchipelagoClient(directory, logs.Add);
        ClientSessionTestHelpers.AttachConnection(client, session);
        ClientSessionTestHelpers.AttachState(client, state);
        ClientSessionTestHelpers.SetLoggedIn(client, true);
        typeof(ArchipelagoClient).GetProperty(nameof(ArchipelagoClient.StageRewardCheckCount))!.SetValue(client, 20);
        var info = (Dictionary<long, StageRewardItemInfo>)Field("_stageRewardInfo").GetValue(client)!;
        foreach (var id in StageRewardChecks.ActiveIds(20))
            info[id] = new StageRewardItemInfo(
                id,
                "Test reward",
                "Player1",
                ArchipelagoClient.Game,
                ItemFlags.Advancement,
                true
            );
        var slots = new[] { false, true };
        try
        {
            Assert(
                client.PrepareStageRewardOffer("no-aether", new[] { false, false }, 0, 0, false) == null
                    && state.StageRewardPity == 0
                    && state.LastStageRewardAttemptRoom == null,
                "screens without Aether do not consume cooldown"
            );
            var nextInfo = info[911404];
            info.Remove(911404);
            Assert(
                client.PrepareStageRewardOffer("not-scouted", slots, 0, 0, false) == null && state.StageRewardPity == 0,
                "missing scout data does not consume cooldown"
            );
            info[911404] = nextInfo;
            ClientSessionTestHelpers.SetLoggedIn(client, false);
            Assert(
                client.PrepareStageRewardOffer("disconnected", slots, 0, 0, false) == null
                    && state.StageRewardPity == 0,
                "disconnected reward screens do not consume cooldown"
            );
            ClientSessionTestHelpers.SetLoggedIn(client, true);

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                var room = $"run-1/room-{attempt}";
                Assert(
                    client.PrepareStageRewardOffer(room, slots, 0, 0, false) == null
                        && state.StageRewardPity == attempt,
                    "the fourth check waits for three distinct eligible rooms even on the best roll"
                );
                state = ApState.Open(directory, identity);
                ClientSessionTestHelpers.AttachState(client, state);
                Assert(
                    client.PrepareStageRewardOffer(room, slots, 0, 0, false) == null
                        && state.StageRewardPity == attempt,
                    "reload restores the cooldown without another roll or increment"
                );
            }

            var offer = client.PrepareStageRewardOffer("run-1/room-4", slots, 19, 0, false);
            Assert(
                offer == new StageRewardOfferDecision(911404, 4, 1),
                "the real client uses cursor-dependent pacing and the original 20 percent boundary"
            );
            state = ApState.Open(directory, identity);
            ClientSessionTestHelpers.AttachState(client, state);
            Assert(
                client.PrepareStageRewardOffer("run-1/room-4", slots, 99, 0, false) == offer
                    && state.StageRewardPity == 4,
                "a saved offer survives reconnect regardless of the next random roll"
            );
            Assert(
                client.PrepareStageRewardOffer("run-1/room-4", new[] { true, false }, 99, 0, false)
                    == new StageRewardOfferDecision(911404, 4, 0)
                    && state.StageRewardPity == 4,
                "a replay can relocate its saved Aether slot without advancing the cooldown"
            );
            client.DeclineStageReward(911404);
            Assert(
                client.PrepareStageRewardOffer("run-1/room-4", slots, 0, 0, false) == null
                    && state.StageRewardPity == 4,
                "a declined reward cannot be rerolled in the same room"
            );
            Assert(
                client.PrepareStageRewardOffer("run-1/room-5", slots, 99, 0, false) == null
                    && state.StageRewardPity == 5,
                "declining retains the chance phase instead of restarting cooldown"
            );
            state = ApState.Open(directory, identity);
            ClientSessionTestHelpers.AttachState(client, state);
            Assert(
                client.PrepareStageRewardOffer("run-1/room-5", slots, 0, 0, false) == null
                    && state.StageRewardPity == 5,
                "a failed chance roll is also stable after reload"
            );
            Assert(
                client.PrepareStageRewardOffer("run-1/room-6", slots, 39, 0, false)
                    == new StageRewardOfferDecision(911404, 6, 1),
                "the next room continues with a 40 percent chance"
            );
            Assert(
                state.CompleteStageReward(911404) && state.StageRewardPity == 0,
                "completing the offered check resets the full interval"
            );
            Assert(
                client.PrepareStageRewardOffer("run-1/room-6", slots, 0, 0, false) == null
                    && state.StageRewardPity == 0,
                "replaying the completed room cannot start the next check's interval"
            );
            Assert(
                client.PrepareStageRewardOffer("run-2/room-1", slots, 0, 0, false) == null
                    && state.StageRewardPity == 1,
                "the next run starts the fifth check's cooldown without clearing progress"
            );

            state.SetStageRewardOffer("run-2/room-1", 911405, 0);
            state = ApState.Open(directory, identity);
            ClientSessionTestHelpers.AttachState(client, state);
            Assert(
                client.PrepareStageRewardOffer("run-2/room-1", new[] { true, false }, 99, 0, false)
                    == new StageRewardOfferDecision(911405, 1, 0),
                "an existing pre-change offer is retained even inside the new cooldown"
            );
            Assert(
                logs.Count(message => message.StartsWith("[APStageReward] Attempt location=")) == 7,
                "pacing diagnostics log eligible attempts, not reloads or ineligible screens"
            );
        }
        finally
        {
            client.Disconnect();
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        Console.WriteLine(
            "PASS: Stage Reward client cooldown, eligibility, replay, reconnect and legacy offer preservation"
        );
    }

    private static FieldInfo Field(string name) =>
        typeof(ArchipelagoClient).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception("Stage Reward pacing: " + message);
    }
}

public class StageRewardSocketProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        targetMethod?.Name switch
        {
            "get_Connected" => true,
            "DisconnectAsync" => Task.CompletedTask,
            _ => throw new InvalidOperationException("Unexpected test socket call: " + targetMethod?.Name),
        };
}
