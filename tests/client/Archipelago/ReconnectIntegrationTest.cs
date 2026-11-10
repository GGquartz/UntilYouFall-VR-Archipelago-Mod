using System.Reflection;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Packets;
using UntilYouFallArchipelago;

internal static class ReconnectIntegrationTest
{
    public static async Task Run(string server, string slot)
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-sync-audit-" + Guid.NewGuid());
        var notices = new List<ApNotice>();
        var logs = new List<string>();
        var inRun = false;
        var lostConnections = 0;
        var heals = 0;
        var runEffects = 0;
        var client = Create();
        try
        {
            client.FirstKill(910301);
            Check(
                logs.Any(line => line.Contains("connect to an AP slot before collecting")),
                "before first login, checks are explicitly ignored"
            );
            client.Connect(server, slot, "");
            await Until(() => client.Connected);
            await Settle();
            Check(
                client.ConfirmedChecks == 0,
                "destructive sync audit requires a fresh disposable server, never a playing room"
            );
            Consume();
            Check(client.PendingItems == 0, "initial history can be consumed with game callbacks ready");
            var starter = client.StartingWeaponIds[0];
            var initialHandled = client.HandledCount;
            var initialNotices = notices.Count;
            client.Disconnect();
            Check(
                lostConnections == 0 && client.WeaponUnlocks.Contains(starter),
                "explicit disconnect preserves ownership without reporting a network drop"
            );
            client.ClearStage(1, new[] { WeaponCatalog.ById(starter)! });
            client.ClearStage(1, new[] { WeaponCatalog.ById(starter)! });
            client.FirstKill(910301);
            client.BuyShopItem(1, 1);
            var stageReward = StageRewardChecks.LocationId(1);
            State().SetStageRewardOffer("sync-audit-room", stageReward, 0);
            Check(client.CompleteStageReward(stageReward), "an already offered Stage Reward can be completed offline");
            client.QuestRunStarted();
            client.QuestAether(350);
            if (client.ActiveQuests.Length > 0)
            {
                var quest = client.ActiveQuests[0];
                client.ObserveQuestProgress(QuestChecks.All[quest].Source, QuestChecks.All[quest].Target);
                Check(client.QuestDone(quest), "quest completion records reward checks while offline");
            }
            var state = State();
            var offlineChecks = state.Checks.ToArray();
            var progress = state.QuestProgress.ToDictionary();
            Check(
                state.QuestRun.Active && offlineChecks.Length >= 4,
                "offline stage/enemy/shop/reward checks and run state are persisted"
            );
            client = Create();
            Check(
                client.WeaponUnlocks.Count == 0 && !client.QuestRunActive,
                "new process does not restore an AP identity before login"
            );
            client.Connect(server, slot, "");
            await Until(() => client.Connected && offlineChecks.All(Confirmed));
            await Settle();
            Consume();
            Check(
                client.QuestRunActive && progress.All(entry => client.QuestProgress(entry.Key) == entry.Value),
                "reconnect restores persisted quest progress and the active run"
            );
            Check(
                client.HandledCount >= initialHandled && client.WeaponUnlocks.Contains(starter),
                "reconnect restores starter ownership and the item cursor"
            );
            var afterOfflineNotices = notices.Count;
            var handledBeforeReplay = client.HandledCount;
            client.Disconnect();
            client.Connect(server, slot, "");
            await Until(() => client.Connected);
            await Settle();
            Consume();
            Check(
                client.HandledCount == handledBeforeReplay && notices.Count == afterOfflineNotices,
                "full history replay does not repeat consumed effects or notifications"
            );

            var session = Session();
            await session.Socket.DisconnectAsync();
            await Until(() => lostConnections == 1);
            client.FirstKill(910302);
            Check(State().Checks.Contains(910302), "unexpected socket loss still permits durable offline checks");
            client.Connect(server, slot, "");
            client.FirstKill(910303);
            Check(
                ApState.Open(directory, state.Identity).Checks.Contains(910303),
                "a check during login is persisted in the last validated context"
            );
            await Until(() => client.Connected && Confirmed(910302));
            await Until(() => Confirmed(910303));
            Check(true, "login-window check is submitted to the server");

            session = Session();
            var pendingBefore = client.PendingItems;
            Cheat(session, "Shard of Vitality");
            await Until(() => client.PendingItems > pendingBefore);
            var beforeHeal = client.HandledCount;
            Consume(ready: false);
            Check(client.HandledCount == beforeHeal, "menu/loading health gate keeps a new Shard pending");
            var upgrades = client.WeaponUpgradeItemCount ?? 0;
            Cheat(session, "Progressive Max Weapon Upgrade Level");
            await Until(() => client.WeaponUpgradeItemCount > upgrades);
            Check(
                client.HandledCount == beforeHeal,
                "permanent upgrades synchronize even behind a deferred consumable"
            );
            Consume();
            Check(heals > 0 && client.PendingItems == 0, "return to a ready player consumes deferred items");

            pendingBefore = client.PendingItems;
            Cheat(session, "Super Charge");
            await Until(() => client.PendingItems > pendingBefore);
            inRun = true;
            Consume();
            Check(runEffects == 0, "a run-only item received outside a run stays void after entering combat");
            pendingBefore = client.PendingItems;
            Cheat(session, "Enemy Stasis");
            await Until(() => client.PendingItems > pendingBefore);
            Consume(ready: false);
            Check(runEffects == 0, "loading defers a run-only item without applying it");
            Consume();
            Check(runEffects == 1, "a run-only item received in a run applies once after loading");

            client.Disconnect();
            await SendWhileOffline("Shard of Vitality");
            var beforeOfflineDelivery = heals;
            client.Connect(server, slot, "");
            await Until(() => client.Connected && client.PendingItems > 0);
            Consume();
            Check(heals == beforeOfflineDelivery + 1, "an item sent while offline arrives on reconnect exactly once");
            var afterDelivery = client.HandledCount;
            var noticesAfterDelivery = notices.Count;
            client.Disconnect();
            client.Connect(server, slot, "");
            client.Connect(server, slot, "");
            await Until(() => client.Connected);
            await Settle();
            Consume();
            Check(
                client.HandledCount == afterDelivery
                    && heals == beforeOfflineDelivery + 1
                    && notices.Count == noticesAfterDelivery,
                "rapid successive logins ignore stale session events and history"
            );

            var weapon = WeaponCatalog.All.First(entry => entry.Id != starter && entry.Id != 910010);
            session = Session();
            foreach (
                var item in new[]
                {
                    weapon.Name,
                    "Left Hand",
                    "Right Hand",
                    "Extra Trait Slot: " + weapon.Name,
                    weapon.Super,
                    "Progressive Shop Unlock",
                    "Progressive Aether Multiplier",
                    "Player Bonus: Maximum Health +1",
                    weapon.Name + " Health Damage +1",
                    "Player Bonus: +10% Critical Strike Window Length",
                    "Player Bonus: -10% Dash Recharge Time",
                    "Difficulty: " + DifficultySettings.Names[client.DifficultyConfig!.Maximum],
                }
            )
                Cheat(session, item);
            await Until(() =>
                client.WeaponUnlocks.Contains(weapon.Id)
                && client.HandUnlocks.Contains(ApItems.LeftHandId)
                && client.HandUnlocks.Contains(ApItems.RightHandId)
                && client.TraitSlotUnlocks.Contains(weapon.TraitSlotItemId)
                && client.WeaponAbilityUnlocks.Contains(weapon.SuperItemId)
                && client.ShopUnlockCount > 0
                && client.AetherMultiplierItemCount > 0
                && client.PlayerBonusItemCounts.ContainsKey(ApItems.MaximumHealthBonusId)
                && client.PlayerBonusItemCounts.ContainsKey(ApItems.CriticalWindowBonusId)
                && client.PlayerBonusItemCounts.ContainsKey(ApItems.DashRechargeBonusId)
                && client.PlayerBonusItemCounts.ContainsKey(
                    WeaponDamageBonusRules.ItemId(weapon.Id, WeaponDamageKind.Health)
                )
                && client.DifficultyUnlocks.Contains(DifficultySettings.ItemId(client.DifficultyConfig!.Maximum))
            );
            await Settle();
            Consume();
            var owned = client.WeaponUnlocks.ToHashSet();
            var hands = client.HandUnlocks.ToHashSet();
            var traits = client.TraitSlotUnlocks.ToHashSet();
            var abilities = client.WeaponAbilityUnlocks.ToHashSet();
            var difficulties = client.DifficultyUnlocks.ToHashSet();
            var bonuses = client.PlayerBonusItemCounts.ToDictionary();
            var shop = client.ShopUnlockCount;
            var multiplier = client.AetherMultiplierItemCount;
            var cap = client.WeaponUpgradeItemCount;
            long? hintedReward = null;
            var hintQuest = client.ActiveQuests.Where(quest => !client.QuestDone(quest)).Take(1).ToArray();
            if (hintQuest.Length > 0)
            {
                hintedReward = QuestChecks.LocationId(hintQuest[0], 1);
                await session.Locations.ScoutLocationsAsync(
                    HintCreationPolicy.CreateAndAnnounceOnce,
                    hintedReward.Value
                );
                await Until(() => client.QuestRewardText(hintedReward.Value) != null);
                Check(true, "server-side hints reveal the matching quest reward row");
            }
            client.Disconnect();
            Check(
                client.AetherMultiplierItemCount == multiplier && client.ShopUnlockCount == shop,
                "client retains multiplier and shop ownership while disconnected"
            );
            client.Connect(server, slot, "");
            Check(
                client.AetherMultiplierItemCount == multiplier
                    && client.ShopUnlockCount == shop
                    && client.StartingHands.HasValue
                    && client.WeaponUpgradeItemCount == cap
                    && client.PlayerBonusItemCounts.Count == bonuses.Count,
                "pending login retains the complete permanent snapshot"
            );
            await Until(() => client.Connected && client.ShopUnlockCount == shop);
            client.Disconnect();
            client = Create();
            client.Connect(server, slot, "");
            await Until(() => client.Connected && client.ShopUnlockCount == shop);
            await Settle();
            Consume();
            Check(
                owned.SetEquals(client.WeaponUnlocks)
                    && hands.SetEquals(client.HandUnlocks)
                    && traits.SetEquals(client.TraitSlotUnlocks)
                    && abilities.SetEquals(client.WeaponAbilityUnlocks)
                    && difficulties.SetEquals(client.DifficultyUnlocks)
                    && bonuses.Count == client.PlayerBonusItemCounts.Count
                    && bonuses.All(entry => client.PlayerBonusItemCounts.GetValueOrDefault(entry.Key) == entry.Value)
                    && client.AetherMultiplierItemCount == multiplier
                    && client.WeaponUpgradeItemCount == cap,
                "all durable unlock and stat categories survive process recreation without doubling"
            );
            if (hintedReward.HasValue)
            {
                await Until(() => client.QuestRewardText(hintedReward.Value) != null);
                Check(true, "server hint revelation is restored after process recreation");
            }

            client.QuestRunFinished(false, 0);
            var runsBeforeOfflineStart = State().GoalRuns;
            client.Disconnect();
            client = Create();
            client.QuestRunStarted();
            client.RecordRewardKindsUsed(true, true, true);
            client.QuestAether(350);
            var failuresBefore = logs.Count(line => line.Contains("Login failed:"));
            client.Connect(server, "__UYF_sync_audit_missing_slot__", "");
            await Until(() => logs.Count(line => line.Contains("Login failed:")) > failuresBefore);
            Check(client.QuestRunActive, "a failed first login retains the observed unbound run");
            client.Connect(server, slot, "");
            await Until(() => client.Connected);
            await Settle();
            Check(
                client.QuestRunActive
                    && State().QuestRun.UsedVitality
                    && State().QuestRun.UsedResolve
                    && State().QuestRun.UsedAether,
                "first login recovers a running lifecycle including shard and aether choices"
            );
            client.QuestRunFinished(true, 100);
            Check(
                State().GoalRuns == runsBeforeOfflineStart + 1,
                "a run started before first login increments the goal counter once"
            );
            client.QuestRunFinished(true, 100);
            Check(State().GoalRuns == runsBeforeOfflineStart + 1, "duplicate run finish is ignored");

            var savedIdentity = State().Identity;
            failuresBefore = logs.Count(line => line.Contains("Login failed:"));
            client.Connect(server, "__UYF_sync_audit_missing_slot__", "");
            await Until(() => logs.Count(line => line.Contains("Login failed:")) > failuresBefore);
            client.FirstKill(910304);
            Check(
                ApState.Open(directory, savedIdentity).Checks.Contains(910304),
                "a rejected reconnect preserves durable offline recording"
            );
            client.Connect(server, slot, "");
            await Until(() => client.Connected);
            await Settle();

            var previousState = State();
            var previousMultiplier = client.AetherMultiplierItemCount;
            var otherEndpoint = server.Replace("127.0.0.1", "localhost");
            Check(otherEndpoint != server, "identity isolation test needs a 127.0.0.1 endpoint");
            client.Connect(otherEndpoint, slot, "");
            client.FirstKill(910305);
            await Until(() => client.Connected);
            await Settle();
            Check(
                State().Identity != savedIdentity
                    && !State().Checks.Contains(910305)
                    && !Confirmed(910305)
                    && ApState.Open(directory, savedIdentity).Checks.Contains(910305),
                "switching AP identity keeps handshake checks in the previous context only"
            );
            Cheat(Session(), "Progressive Aether Multiplier");
            await Until(() => client.AetherMultiplierItemCount > previousMultiplier);
            Check(
                ApState.Open(directory, savedIdentity).AetherMultiplierItemCount == previousMultiplier
                    && previousState.AetherMultiplierItemCount == previousMultiplier,
                "candidate item packets cannot mutate the previous identity"
            );
            client.Connect(server, slot, "");
            await Until(() => client.Connected && Confirmed(910305));
            Check(State().Identity == savedIdentity, "returning to the original identity submits its offline checks");
            client.Disconnect();
            switch (client.Goal!.Mode)
            {
                case GoalMode.Quests:
                    client.DebugCompleteQuests();
                    break;
                case GoalMode.AllWeapons:
                    client.ClearStage(StageChecks.Count, WeaponCatalog.All);
                    break;
                default:
                    for (var run = 0; run < client.Goal.Runs; run++)
                        client.DebugGoalRun();
                    break;
            }
            Check(
                !(bool)ClientSessionTestHelpers.GetField(ClientSessionTestHelpers.Progress(client), "_goalSent"),
                "completing the goal offline does not attempt a network send"
            );
            client.Connect(server, slot, "");
            await Until(() =>
                client.Connected
                && (bool)ClientSessionTestHelpers.GetField(ClientSessionTestHelpers.Progress(client), "_goalSent")
            );
            await Settle();
            Check(
                logs.Any(line => line.Contains("[APGoal] Goal sent")),
                "an offline-completed goal is submitted after reconnect"
            );
            Check(
                !logs.Any(line => line.Contains("Processing stopped") || line.Contains("Connection failed")),
                "the exercised packet paths have no client processing errors"
            );
            Console.WriteLine($"PASS: sync regression finished; notices={initialNotices}->{notices.Count}");
        }
        finally
        {
            client.Disconnect();
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

        ArchipelagoClient Create()
        {
            var result = new ArchipelagoClient(
                directory,
                message =>
                {
                    logs.Add(message);
                    Console.WriteLine(message);
                },
                notices.Add,
                canReceiveRunOnlyEffects: () => inRun
            );
            result.ConnectionLost = () => lostConnections++;
            return result;
        }

        ApState State() => (ApState)Field("_state").GetValue(client)!;
        ArchipelagoSession Session() => ClientSessionTestHelpers.Connection(client).Session!;
        bool Confirmed(long location) =>
            ClientSessionTestHelpers.Progress(client).ConfirmedLocationIds.Contains(location);

        void Consume(bool ready = true)
        {
            for (var attempt = 0; attempt < 1000 && client.PendingItems > 0; attempt++)
            {
                var before = client.HandledCount;
                client.ApplyNextItem(
                    new ItemEffects
                    {
                        CanHeal = () => ready,
                        Heal = _ => heals++,
                        ApplyAetherFiller = (_, _) => ready,
                        CanSpawnEnemy = () => ready,
                        SpawnEnemy = () => { },
                        DodgeOnlyTrap = () => { },
                        ExhaustionTrap = () => { },
                        SuperCharge = () => runEffects++,
                        EnemyStasis = () => runEffects++,
                    }
                );
                if (client.HandledCount == before)
                    break;
            }
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
            throw new Exception("Timed out waiting for sync audit condition; inspect preceding client logs.");
        }

        async Task Settle()
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(600);
            while (DateTime.UtcNow < deadline)
            {
                client.Update();
                await Task.Delay(25);
            }
        }

        async Task SendWhileOffline(string item)
        {
            var actor = ArchipelagoSessionFactory.CreateSession(server);
            try
            {
                var login = await Task.Run(() =>
                    actor.TryConnectAndLogin(
                        ArchipelagoClient.Game,
                        slot,
                        ItemsHandlingFlags.AllItems,
                        new Version(0, 6, 7)
                    )
                );
                Check(login.Successful, "isolated sender can log into the disconnected test slot");
                var received = actor.Items.AllItemsReceived.Count;
                Cheat(actor, item);
                var deadline = DateTime.UtcNow.AddSeconds(10);
                while (actor.Items.AllItemsReceived.Count <= received && DateTime.UtcNow < deadline)
                    await Task.Delay(25);
                Check(
                    actor.Items.AllItemsReceived.Count == received + 1,
                    "server stores an item while the tested client is offline"
                );
            }
            finally
            {
                await actor.Socket.DisconnectAsync();
            }
        }
    }

    private static void Cheat(ArchipelagoSession session, string item) =>
        session.Socket.SendPacket(new SayPacket { Text = "!getitem " + item });

    private static FieldInfo Field(string name) =>
        typeof(ArchipelagoClient).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
        Console.WriteLine("PASS: " + message);
    }
}
