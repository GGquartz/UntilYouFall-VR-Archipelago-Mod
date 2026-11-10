using System.Reflection;
using UntilYouFallArchipelago;

internal static class ReconnectStateTests
{
    public static void Run()
    {
        AutomaticTransport();
        var directory = Path.Combine(Path.GetTempPath(), "uyf-reconnect-state-" + Guid.NewGuid());
        var client = new ArchipelagoClient(directory, _ => { });
        try
        {
            var state = ApState.Open(directory, "validated-slot");
            state.SetAetherMultiplierItemCount(4);
            state.SetWeaponUpgradeItemCount(2);
            var weapon = WeaponCatalog.All[0];
            state.UnlockWeapon(weapon.Id);
            state.UnlockHand(ApItems.RightHandId);
            state.UnlockTraitSlot(weapon.TraitSlotItemId);
            state.UnlockWeaponAbility(weapon.SuperItemId);
            state.UnlockDifficulty(DifficultySettings.FirstItemId);
            state = ApState.Open(directory, state.Identity);
            ClientSessionTestHelpers.AttachState(client, state);
            Property("StartingHands").SetValue(client, StartingHands.RightOnly);
            Property("StartingWeaponIds").SetValue(client, new[] { 910019L, 910014L });
            Property("SeparateWeaponSupers").SetValue(client, true);
            var history = (ReceivedItemHistory)Field("_receivedItemHistory").GetValue(client)!;
            history.Record(0, ApItems.ProgressiveShopUnlockId);
            client.Connect("ws://127.0.0.1:1", "Missing", "");
            Check(ReferenceEquals(Field("_state").GetValue(client), state), "login must retain the validated state");
            Check(
                client.WeaponUnlocks.SequenceEqual(new[] { weapon.Id })
                    && client.HandUnlocks.SequenceEqual(new[] { ApItems.RightHandId })
                    && client.TraitSlotUnlocks.SequenceEqual(new[] { weapon.TraitSlotItemId })
                    && client.WeaponAbilityUnlocks.SequenceEqual(new[] { weapon.SuperItemId })
                    && client.DifficultyUnlocks.SequenceEqual(new[] { DifficultySettings.FirstItemId }),
                "client ownership must come directly from persisted ApState without received-item caches"
            );
            Check(
                client.StartingHands == StartingHands.RightOnly
                    && client.StartingWeaponIds.SequenceEqual(new[] { 910019L, 910014L })
                    && client.SeparateWeaponSupers
                    && client.WeaponUpgradeItemCount == 2
                    && client.ShopUnlockCount == 1
                    && client.AetherMultiplierItemCount == 4,
                "login must retain restrictions and permanent counts"
            );
            client.FirstKill(910301);
            Check(ApState.Open(directory, state.Identity).Checks.Contains(910301), "login-window check must persist");
            client.Disconnect();
            client.FirstKill(910302);
            Check(state.Checks.Contains(910302), "cancelled login must keep offline checks working");
            var epoch = (int)Field("_runOnlyEffectEpoch").GetValue(client)!;
            client.VoidRunOnlyEffects();
            Check(
                (int)Field("_runOnlyEffectEpoch").GetValue(client)! != epoch,
                "ending a run invalidates eligibility captured by staged session packets"
            );
            client.RecordRunOnlyEffectReceipt(0, ApItems.EnemyStasisId, false);
            Check(state.VoidedRunOnlyEffects.Contains(0), "staged receipt keeps its original ineligible state");
        }
        finally
        {
            client.Disconnect();
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
        UnboundRunLifecycle();
        Console.WriteLine("PASS: reconnect retains validated context, restrictions and offline checks");
    }

    private static void AutomaticTransport()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-transport-" + Guid.NewGuid());
        var client = new ArchipelagoClient(directory, _ => { });
        try
        {
            foreach (
                var (address, scheme, port) in new[]
                {
                    ("127.0.0.1:1", "unspecified", 1),
                    ("127.0.0.1:1/", "unspecified", 1),
                    ("ws://127.0.0.1:1", "ws", 1),
                    ("WSS://127.0.0.1:1", "wss", 1),
                }
            )
            {
                client.Connect(address, "Missing", "");
                var session = ClientSessionTestHelpers.Connection(client).Session!;
                Check(
                    session.Socket.Uri.Scheme == scheme && session.Socket.Uri.Port == port,
                    $"transport and port must be preserved for {address}"
                );
                client.Disconnect();
            }
            var defaultPort = Archipelago.MultiClient.Net.ArchipelagoSessionFactory.CreateSession("localhost");
            Check(defaultPort.Socket.Uri.Port == 38281, "the SDK must retain its AP default port for bare hosts");
            Check(
                UntilYouFall.Shared.LauncherProtocol.ConnectCommand("archipelago.gg:58880", "Player1", "").Split('\n')[
                    1
                ] == "archipelago.gg:58880",
                "the launcher command must retain a bare address"
            );
            foreach (
                var invalid in new[]
                {
                    "host:invalid",
                    "ftp://localhost",
                    "unspecified://localhost",
                    "ws://host:invalid",
                }
            )
            {
                var rejected = false;
                try
                {
                    client.Connect(invalid, "Missing", "");
                }
                catch (Exception exception) when (exception is ArgumentException or UriFormatException)
                {
                    rejected = true;
                }
                Check(rejected, $"invalid or internal addresses must be rejected before connecting: {invalid}");
            }
        }
        finally
        {
            client.Disconnect();
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private static void UnboundRunLifecycle()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-unbound-run-" + Guid.NewGuid());
        var client = new ArchipelagoClient(directory, _ => { });
        try
        {
            client.QuestRunFinished(true, 10);
            Check(!client.QuestRunActive, "a missing run start cannot be invented");
            client.QuestRunStarted();
            client.QuestAether(100);
            client.RecordRewardKindsUsed(true, false);
            client.QuestRunFinished(false, 50);
            client.QuestRunStarted();
            client.QuestAether(200);
            client.RecordRewardKindsUsed(false, true, true);
            client.QuestRunFinished(true, 2000);
            client.QuestRunFinished(true, 2000);
            var state = ApState.Open(directory, "first-validated-slot");
            ClientSessionTestHelpers.AttachState(client, state);
            ClientSessionTestHelpers.SetSettings(
                client,
                "ActiveQuests",
                Enumerable.Range(0, QuestChecks.All.Length).ToArray()
            );
            var events = (Queue<Action>)Field("_unboundRunEvents").GetValue(client)!;
            while (events.TryDequeue(out var apply))
                apply();
            int Progress(QuestSource source) =>
                client.QuestProgress(Array.FindIndex(QuestChecks.All, quest => quest.Source == source));
            Check(
                state.GoalRuns == 1 && !client.QuestRunActive,
                "unbound loss and victory replay in order exactly once"
            );
            Check(
                Progress(QuestSource.NoVitality) == 1
                    && Progress(QuestSource.NoResolve) == 0
                    && Progress(QuestSource.NoAetherReward) == 0,
                "shard flags reset only at the next observed run start"
            );
            Check(
                Progress(QuestSource.AetherTotal) == 300 && Progress(QuestSource.AetherRun) == 200,
                "unbound aether uses per-run maxima and lifetime baselines"
            );
        }
        finally
        {
            client.Disconnect();
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private static FieldInfo Field(string name) =>
        typeof(ArchipelagoClient).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static PropertyInfo Property(string name) => typeof(ArchipelagoClient).GetProperty(name)!;

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
