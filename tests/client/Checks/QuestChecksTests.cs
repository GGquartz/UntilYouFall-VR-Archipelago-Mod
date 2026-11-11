using System.Reflection;
using UntilYouFallArchipelago;

internal static class QuestChecksTests
{
    public static void Run()
    {
        HintCosts();
        UnlockGate();
        CatalogSources();
        var wings = QuestChecks.All.Single(quest => quest.Name == "On the Wings of the Knight");
        Assert(
            wings.Need.Weapons.Length == 0
                && wings.Need.AnyWeapons.Length == 0
                && wings.Need.AnyCount == 0
                && !QuestChecks.IsClearQuest(wings),
            "Wings is a launch quest without a weapon requirement"
        );
        foreach (
            var counter in new[] { "Burning Retribution", "Cold Iron Verdict", "Shatter Storm", "Unbreakable Wall" }
        )
            Assert(
                !QuestChecks.IsClearQuest(QuestChecks.All.Single(quest => quest.Name == counter)),
                counter + " is a counter quest and must not complete on a stage clear"
            );
        Assert(
            QuestChecks.IsClearQuest(QuestChecks.All.Single(quest => quest.Name == "Katarstrophy")),
            "Katarstrophy is a clear quest"
        );
        var parry = QuestChecks.All.Single(quest => quest.Name == "Parry Party");
        Assert(
            parry.Target == 250 && parry.Description == "Successfully block 250 melee attacks",
            "Parry Party uses a consistent 250-block target and description"
        );
        var titan = QuestChecks.All.Single(quest => quest.Name == "Titan's Grip");
        Assert(
            QuestChecks.ClearMatches(titan, 21, new[] { "Heartsplitter", "Mistcutter" }),
            "two heavy weapons clear Titan's Grip"
        );
        Assert(
            !QuestChecks.ClearMatches(titan, 21, new[] { "Heartsplitter", "Ritual Dagger" }),
            "one heavy weapon does not"
        );
        var katars = QuestChecks.All.Single(quest => quest.Name == "Katarstrophy");
        Assert(!QuestChecks.ClearMatches(katars, 21, new[] { "Gemini Stranded" }), "both required weapons are needed");
        var monk = QuestChecks.All.Single(quest => quest.Name == "Monk of Rokar");
        var monkWeapons = new[] { "Knight-Blooded Crest", "Spellsword's Charm" };
        Assert(
            QuestChecks.ClearMatches(monk, 14, monkWeapons) && !QuestChecks.ClearMatches(monk, 13, monkWeapons),
            "Monk of Rokar clears from stage 14"
        );
        Assert(
            QuestChecks
                .KillQuestSources("Empowered Captain")
                .SequenceEqual(
                    new[] { QuestSource.EnemiesKilled, QuestSource.EmpoweredsKilled, QuestSource.CaptainsKilled }
                ),
            "empowered kills count for base type"
        );
        Assert(
            QuestChecks
                .KillQuestSources("Aether Horror")
                .SequenceEqual(new[] { QuestSource.EnemiesKilled, QuestSource.HorrorStages }),
            "Aether Horror combat deaths count for Extermin-aether"
        );
        Assert(
            !QuestChecks.KillQuestSources("Rokari Honor Guard").Contains(QuestSource.HorrorStages),
            "other enemies do not count as Aether Horror deaths"
        );
        Assert(
            QuestChecks.LocationId(0, 1) == 912001 && QuestChecks.Name(0, 1) == "Quest: Unleashed Potential - Reward 1",
            "stable ids"
        );
        Assert(
            QuestChecks
                .ReadActive(new Dictionary<string, object> { ["quests"] = new object[] { 1, 5 } })
                .SequenceEqual([1, 5]),
            "slot data parses"
        );
        LocalProgress();
        FinalHorrorProgress();
        CrowdControlProgress();
    }

    private static void CrowdControlProgress()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-crowd-control-" + Guid.NewGuid());
        var client = new ArchipelagoClient(directory, _ => { });
        ClientSessionTestHelpers.AttachState(client, ApState.Open(directory, "crowd-control/seed/slot1"));
        var index = Array.FindIndex(QuestChecks.All, quest => quest.Source == QuestSource.KillsInWindow);
        ClientSessionTestHelpers.SetSettings(client, "ActiveQuests", new[] { index });
        var quest = QuestChecks.All[index];
        var originalTarget = quest.Target;
        var recentKills = new Queue<float>();
        try
        {
            quest.Target = 4;
            foreach (var seconds in new[] { 0f, 1f, 2f })
                client.ObserveQuestProgress(
                    QuestSource.KillsInWindow,
                    QuestChecks.RecordKillInWindow(recentKills, seconds, windowSeconds: 5f)
                );
            Assert(client.QuestProgress(index) == 3 && !client.QuestDone(index), "the catalog can require four kills");
            var count = QuestChecks.RecordKillInWindow(recentKills, nowSeconds: 5f, windowSeconds: 5f);
            Assert(count == 4, "kills exactly five seconds apart remain in the window");
            client.ObserveQuestProgress(QuestSource.KillsInWindow, count);
            Assert(
                client.QuestProgress(index) == 4 && client.QuestDone(index),
                "four kills complete the catalog target"
            );
            Assert(
                QuestChecks.RecordKillInWindow(recentKills, nowSeconds: 6.01f, windowSeconds: 5f) == 3,
                "only kills outside the rolling window expire"
            );
        }
        finally
        {
            quest.Target = originalTarget;
            Directory.Delete(directory, true);
        }
    }

    private static void CatalogSources()
    {
        var counters = Enum.GetValues<QuestSource>().Where(source => source != QuestSource.None).ToHashSet();
        foreach (var quest in QuestChecks.All)
        {
            Assert(quest.Icon.Length > 0, quest.Name + " has an icon");
        }
        var sources = QuestChecks
            .All.Where(quest => quest.Source != QuestSource.None)
            .Select(quest => quest.Source)
            .ToList();
        Assert(sources.Count == sources.Distinct().Count(), "no two quests share a Source");
        Assert(counters.All(sources.Contains), "every QuestSource counter is used by a quest");
    }

    private static void UnlockGate()
    {
        long Weapon(string name) => WeaponCatalog.ByName(name)!.Id;
        long Ability(string name) => WeaponAbilityCatalog.ItemIds[name];
        var none = Array.Empty<long>();
        var iron = QuestChecks.All.Single(quest => quest.Name == "Cold Iron Verdict");
        var storm = QuestChecks.All.Single(quest => quest.Name == "Shatter Storm");
        var free = QuestChecks.All.Single(quest => quest.Name == "On the Wings of the Knight");

        Assert(QuestChecks.UnlocksOwned(free, none, none, true), "a quest without needs is always tracked");
        Assert(!QuestChecks.UnlocksOwned(iron, none, none, true), "missing weapon blocks the quest");
        var greataxe = new[] { Weapon("Cold Iron Greataxe") };
        Assert(
            !QuestChecks.UnlocksOwned(iron, greataxe, none, true),
            "separate supers require the ability item as well"
        );
        Assert(
            QuestChecks.UnlocksOwned(iron, greataxe, none, false),
            "supers bundled with the weapon need no extra ability"
        );
        Assert(
            QuestChecks.UnlocksOwned(iron, greataxe, new[] { Ability("Vicious Strike") }, true),
            "weapon and ability together unlock the quest"
        );
        var esper = new[] { Weapon("Gemini Esper") };
        var shardstorm = new[] { Ability("Shardstorm") };
        Assert(!QuestChecks.UnlocksOwned(storm, none, shardstorm, true), "any-of needs one of the listed weapons");
        Assert(QuestChecks.UnlocksOwned(storm, esper, shardstorm, true), "one listed weapon is enough");
        Assert(!QuestChecks.UnlocksOwned(storm, esper, none, true), "any-of weapon still needs the ability");
        var superest = QuestChecks.All.Single(quest => quest.Name == "Super-est");
        var sword = new[] { Weapon("Engraved Arming Sword") };
        var swordSuper = new[] { WeaponCatalog.ByName("Engraved Arming Sword")!.SuperItemId };
        Assert(!QuestChecks.UnlocksOwned(superest, none, none, true), "Super-est needs a super");
        Assert(!QuestChecks.UnlocksOwned(superest, sword, none, true), "a weapon without its super item is not enough");
        Assert(
            !QuestChecks.UnlocksOwned(superest, none, swordSuper, true),
            "a super item without its weapon is not enough"
        );
        Assert(QuestChecks.UnlocksOwned(superest, sword, swordSuper, true), "weapon plus its super unlocks Super-est");
        Assert(QuestChecks.UnlocksOwned(superest, none, none, false), "bundled supers need no extra item");
    }

    private static void HintCosts()
    {
        Assert(
            QuestChecks.ReadHintCost(new Dictionary<string, object>()) == 500,
            "legacy seeds keep the original hint price"
        );
        foreach (var cost in new[] { 0, 1, 500, 5000 })
        {
            Assert(
                QuestChecks.ReadHintCost(new Dictionary<string, object> { ["quest_hint_cost"] = (long)cost }) == cost,
                "slot data keeps the configured hint price"
            );
            Assert(
                QuestChecks.HintCostText(cost) == (cost == 0 ? "Hint Cost: Free" : $"Hint Cost: {cost}"),
                "board displays Free only for a zero price"
            );
        }
        foreach (var invalid in new object[] { -1, 5001, 1.5m, true, null! })
        {
            try
            {
                QuestChecks.ReadHintCost(new Dictionary<string, object> { ["quest_hint_cost"] = invalid });
                throw new Exception("invalid hint price was accepted");
            }
            catch (InvalidDataException) { }
        }
        var client = new ArchipelagoClient("unused-test-state", _ => { });
        Assert(client.QuestHintCost == 500, "client starts with the legacy/default price");
    }

    private static void FinalHorrorProgress()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-final-horror-" + Guid.NewGuid());
        const string identity = "final-horror/seed/slot1";
        var client = new ArchipelagoClient(directory, _ => { });
        ClientSessionTestHelpers.AttachState(client, ApState.Open(directory, identity));
        var index = Array.FindIndex(QuestChecks.All, quest => quest.Name == "Extermin-aether");
        ClientSessionTestHelpers.SetSettings(client, "ActiveQuests", new[] { index });
        var horror = EnemyChecks.All.Single(enemy => enemy.Name == "Aether Horror");
        try
        {
            Assert(!new QuestRunState().FinalHorrorCounted, "old saves default to an uncredited final boss");
            client.QuestClear(21, Array.Empty<string>());
            Assert(client.QuestProgress(index) == 0, "no final boss credit without an active run");
            client.QuestRunStarted();
            client.QuestClear(7, Array.Empty<string>());
            client.QuestClear(14, Array.Empty<string>());
            Assert(client.QuestProgress(index) == 0, "early boss stages still need a real death");
            client.QuestClear(21, Array.Empty<string>());
            client.QuestClear(21, Array.Empty<string>());
            client.QuestKill(horror, 21);
            Assert(client.QuestProgress(index) == 1, "final clear without Kill counts once, including a late Kill");
            ClientSessionTestHelpers.AttachState(client, ApState.Open(directory, identity));
            client.QuestClear(21, Array.Empty<string>());
            client.QuestKill(horror, 21);
            Assert(client.QuestProgress(index) == 1, "final boss deduplication survives reloading the AP save");
            client.QuestRunFinished(false, 30f);
            client.QuestClear(21, Array.Empty<string>());
            Assert(client.QuestProgress(index) == 1, "late completion cannot credit an ended run");
            client.QuestRunStarted();
            client.QuestKill(horror, 21);
            client.QuestClear(21, Array.Empty<string>());
            Assert(client.QuestProgress(index) == 2, "native Kill before clear also counts exactly once in a new run");
            client.QuestRunFinished(false, 30f);
            client.QuestRunStarted();
            client.QuestClear(21, Array.Empty<string>());
            Assert(client.QuestProgress(index) == 3 && client.QuestDone(index), "a third run can complete the quest");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static void LocalProgress()
    {
        var directory = Path.Combine(Path.GetTempPath(), "uyf-quest-local-" + Guid.NewGuid());
        const string identity = "quest-local/seed/slot1";
        var client = new ArchipelagoClient(directory, _ => { });
        ClientSessionTestHelpers.AttachState(client, ApState.Open(directory, identity));
        ClientSessionTestHelpers.SetSettings(
            client,
            "ActiveQuests",
            Enumerable.Range(0, QuestChecks.All.Length).ToArray()
        );
        int Progress(string name) =>
            client.QuestProgress(Array.FindIndex(QuestChecks.All, quest => quest.Name == name));
        try
        {
            var horror = EnemyChecks.All.Single(enemy => enemy.Name == "Aether Horror");
            client.QuestClear(7, Array.Empty<string>());
            client.QuestClear(14, Array.Empty<string>());
            client.QuestClear(21, Array.Empty<string>());
            Assert(
                Progress("Extermin-aether") == 0,
                "boss stage clears without an active run cannot count as Horror deaths"
            );
            client.QuestKill(EnemyChecks.All.Single(enemy => enemy.Name == "Rokari Honor Guard"));
            Assert(Progress("Extermin-aether") == 0, "non-Horror kills cannot advance Extermin-aether");
            client.QuestKill(horror);
            Assert(Progress("Extermin-aether") == 1, "actual Horror death advances the quest once");
            client.QuestKill(horror);
            Assert(Progress("Extermin-aether") == 2, "subsequent Horror deaths count beyond the first-kill check");
            var horrorQuest = Array.FindIndex(QuestChecks.All, quest => quest.Name == "Extermin-aether");
            Assert(!client.QuestDone(horrorQuest), "two Horror kills do not complete Extermin-aether");
            client.QuestKill(horror);
            Assert(
                Progress("Extermin-aether") == 3
                    && client.QuestDone(horrorQuest)
                    && QuestChecks.All[horrorQuest].Description == "Defeat 3 Aether Horrors",
                "three Horror kills complete Extermin-aether and match the displayed target"
            );
            client.QuestRunFinished(true, 30f);
            Assert(
                Progress("Ain't Got Time to Heal") == 0 && Progress("Speedrun") == 0,
                "a victory without a recorded run start cannot award run conditions"
            );
            client.QuestRunStarted();
            client.ObserveQuestProgress(QuestSource.LongCombo, 3);
            client.ObserveQuestProgress(QuestSource.LongCombo, 3);
            Assert(Progress("Quick on the Draw") == 3, "combo snapshots must not add together");
            client.ObserveQuestProgress(QuestSource.TraitsOnWeapon, 3);
            client.ObserveQuestProgress(QuestSource.TraitsOnWeapon, 3);
            client.ObserveQuestProgress(QuestSource.TraitsEquipped, 5);
            client.ObserveQuestProgress(QuestSource.TraitsEquipped, 5);
            client.ObserveQuestProgress(QuestSource.SuperLevel4, 2);
            client.ObserveQuestProgress(QuestSource.SuperLevel4, 2);
            client.ObserveQuestProgress(QuestSource.RagdollDistance, 0);
            client.ObserveQuestProgress(QuestSource.RagdollDistance, 0);
            Assert(
                Progress("Tricked Out") == 3
                    && Progress("Cognitive Overload") == 5
                    && Progress("Super-est") == 2
                    && Progress("On the Wings of the Knight") == 0,
                "trait, super and launch measurements use maxima rather than incremental counters"
            );
            client.ObserveQuestProgress(QuestSource.RagdollDistance, 1);
            Assert(
                Progress("On the Wings of the Knight") == 1,
                "a launch that reached ten metres completes the one-shot quest"
            );
            client.AddQuestProgress(QuestSource.MeleeBlocks, 2);
            client.AddQuestProgress(QuestSource.MeleeBlocks, 3);
            Assert(Progress("Parry Party") == 5, "local event counters accumulate");
            client.QuestAether(350);
            client.QuestAether(350);
            client.QuestAether(200);
            Assert(
                Progress("Burning a Hole in your Armor") == 350 && Progress("Fat Stacks of Aether") == 350,
                "duplicate and lower run payouts must not count again"
            );
            client.RecordRewardKindsUsed(true, false);
            ClientSessionTestHelpers.AttachState(client, ApState.Open(directory, identity));
            client.QuestAether(600);
            client.QuestRunFinished(true, 1500f);
            Assert(Progress("Fat Stacks of Aether") == 600, "run aether survives reconnect without duplication");
            Assert(
                Progress("Speedrun") == 1 && Progress("Ain't Got Time to Heal") == 0 && Progress("Sword God") == 1,
                "victory uses persisted shard flags and includes exactly 25 minutes"
            );
            Assert(Progress("Vow of Poverty") == 1, "victory without an Aether reward completes Vow of Poverty");
            client.QuestRunStarted();
            client.QuestAether(250);
            Assert(
                Progress("Burning a Hole in your Armor") == 600 && Progress("Fat Stacks of Aether") == 850,
                "best run stays visible while lifetime earnings add across runs"
            );
            client.RecordRewardKindsUsed(false, true);
            client.QuestRunFinished(false, 30f);
            Assert(Progress("Ain't Got Time to Heal") == 0, "abandon cannot award a campaign clear");
            client.QuestRunFinished(true, 30f);
            Assert(Progress("Ain't Got Time to Heal") == 0, "a closed run cannot be completed a second time");
            client.QuestRunStarted();
            client.QuestRunFinished(true, 1600f);
            Assert(Progress("Ain't Got Time to Heal") == 1, "a new run resets shard usage");
            Assert(
                Progress("Parry Party") == 5 && Progress("Quick on the Draw") == 3,
                "new run preserves lifetime counters and best combo"
            );
            var parry = Array.FindIndex(QuestChecks.All, quest => quest.Name == "Parry Party");
            client.AddQuestProgress(QuestSource.MeleeBlocks, 244);
            Assert(
                Progress("Parry Party") == 249 && !client.QuestDone(parry),
                "Parry Party does not complete at 249 blocks"
            );
            client.AddQuestProgress(QuestSource.MeleeBlocks, 1);
            Assert(
                Progress("Parry Party") == 250
                    && client.QuestDone(parry)
                    && ApState.Open(directory, identity).Checks.Contains(QuestChecks.LocationId(parry, 1)),
                "the 250th block completes Parry Party and persists its reward"
            );
            var saved = ApState.Open(directory, identity);
            var speedrun = Array.FindIndex(QuestChecks.All, quest => quest.Name == "Speedrun");
            Assert(saved.Checks.Contains(QuestChecks.LocationId(speedrun, 3)), "all quest rewards persist offline");
            Assert(!ApState.Open(directory, "another-seed").QuestRun.Active, "run state is isolated by seed");
            ClientSessionTestHelpers.AttachState(client, ApState.Open(directory, "late-run"));
            client.QuestRunStarted();
            client.RecordRewardKindsUsed(false, true);
            client.QuestRunFinished(true, 1500.1f);
            Assert(
                Progress("Speedrun") == 0 && Progress("Ain't Got Time to Heal") == 1 && Progress("Sword God") == 0,
                "a slow victory can complete no-vitality but not Speedrun or a run that used Resolve"
            );
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
