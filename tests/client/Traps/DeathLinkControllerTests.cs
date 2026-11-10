using HarmonyLib;
using Il2CppSG.Claymore.Entities;
using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.NPCs;
using Il2CppSG.Claymore.Player;
using Il2CppSG.Claymore.UI;
using UntilYouFallArchipelago;

internal static class DeathLinkControllerTests
{
    public static void Run()
    {
        TestDeathsDoor();
        foreach (
            var action in new[] { nameof(PauseScreen.HandleGiveUpButton), nameof(PauseScreen.HandleQuitToTitleButton) }
        )
        {
            foreach (var killer in new Enemy?[] { null, new() })
            {
                var logs = new List<string>();
                var harmony = new Harmony();
                var client = new ArchipelagoClient("unused-test-state", _ => { });
                typeof(ArchipelagoClient)
                    .GetProperty(nameof(client.DeathLinkMode))!
                    .SetValue(client, DeathLinkMode.Soft);
                var manager = new LevelManager { Campaign = new() { GUID = StageChecks.CampaignGuid } };
                var health = new PlayerHealth();
                var controller = new DeathLinkController(
                    harmony,
                    client,
                    () => manager,
                    () => health,
                    () => true,
                    _ => { },
                    Array.Empty<(string, Action)>(),
                    logs.Add
                );
                var killed = harmony.Patches[nameof(PlayerDeathController.OnPlayerKilled)].Postfix!.Method;
                void Kill() => killed.Invoke(null, [killer]);
                Assert(
                    harmony.Patches.TryGetValue(action, out var hooks),
                    action + " must be protected at its native entry point"
                );
                hooks.Prefix!.Method.Invoke(null, [typeof(PauseScreen).GetMethod(action)]);
                Kill();
                Assert(
                    !logs.Any(line => line.Contains("[APDeathLink] Sending cause=")),
                    action + " must not send DeathLink"
                );
                typeof(DeathLinkController)
                    .GetField(
                        "_voluntaryExitAt",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                    )!
                    .SetValue(controller, DateTime.MinValue);
                Kill();
                Assert(
                    logs.Count(line => line.Contains("[APDeathLink] Sending cause=")) == 1,
                    "a genuine death after voluntary exit or an exception must still send, including unknown killers"
                );

                typeof(ArchipelagoClient)
                    .GetProperty(nameof(client.DeathLinkMode))!
                    .SetValue(client, DeathLinkMode.Hard);
                health.OnKilled = Kill;
                client.DeathLinkReceived!("other player", "other player died");
                controller.Update();
                Assert(
                    logs.Any(line => line.Contains("Death caused by incoming DeathLink")),
                    "incoming hard DeathLink still suppresses its own death callback"
                );
                Assert(
                    logs.Count(line => line.Contains("[APDeathLink] Sending cause=")) == 1,
                    "hard DeathLink must not echo"
                );
            }
        }
        Console.WriteLine(
            "PASS: DeathLink ignores voluntary exits, expires the exit window and preserves hard loop protection"
        );
    }

    private static void TestDeathsDoor()
    {
        var mode = DeathLinkRules.Read(
            new Dictionary<string, object> { ["death_link"] = true, ["death_link_mode"] = "deaths_door" }
        );
        Assert(mode.ToString() == "DeathsDoor", "the new slot mode must parse without changing existing modes");
        Assert(
            DeathLinkRules.Read(
                new Dictionary<string, object> { ["death_link"] = false, ["death_link_mode"] = "deaths_door" }
            ) == DeathLinkMode.Off,
            "disabled DeathLink must still ignore the selected mode"
        );
        foreach (var startingHealth in new[] { 8f, 1f })
        {
            var logs = new List<string>();
            var notices = new List<ApNotice>();
            var harmony = new Harmony();
            var client = new ArchipelagoClient("unused-test-state", _ => { });
            typeof(ArchipelagoClient).GetProperty(nameof(client.DeathLinkMode))!.SetValue(client, mode);
            var manager = new LevelManager { Campaign = new() { GUID = StageChecks.CampaignGuid } };
            var health = new PlayerHealth { CurrentHealth = startingHealth };
            var controller = new DeathLinkController(
                harmony,
                client,
                () => manager,
                () => health,
                () => !health.IsDead,
                notices.Add,
                Array.Empty<(string, Action)>(),
                logs.Add
            );
            void Receive()
            {
                client.DeathLinkReceived!("other player", "other player died");
                controller.Update();
            }

            Receive();
            Assert(
                health.DeathsDoorEntries == 1 && health.IsOnDeathsDoor && !health.IsDead,
                "full health and 1 HP must both enter the native state without killing"
            );
            Assert(notices.Count == 1 && notices[0].Death, "entry must retain the DeathLink notification/sound type");
            Receive();
            Assert(
                health.DeathsDoorEntries == 1 && notices.Count == 2,
                "a repeated receipt must notify but cannot re-enter or refresh invulnerability"
            );
            Assert(!logs.Any(line => line.Contains("[APDeathLink] Sending cause=")), "entry must not echo DeathLink");
            harmony.Patches[nameof(PlayerDeathController.OnPlayerKilled)].Postfix!.Method.Invoke(null, [null]);
            Assert(
                logs.Count(line => line.Contains("[APDeathLink] Sending cause=")) == 1,
                "a subsequent real death must not inherit hard-mode self-kill suppression"
            );

            manager.Campaign = null;
            Receive();
            Assert(health.DeathsDoorEntries == 1 && notices.Count == 2, "Runeforge/menu receipts are discarded");
            manager.Campaign = new() { GUID = StageChecks.CampaignGuid };
            health = new() { IsDead = true };
            Receive();
            Assert(health.DeathsDoorEntries == 0 && notices.Count == 2, "dead players cannot receive the effect");
            health = new();
            manager.IsLoading = true;
            controller.Update();
            Assert(health.DeathsDoorEntries == 0, "loading must defer a pending effect");
            manager.IsLoading = false;
            controller.Update();
            Assert(
                health.DeathsDoorEntries == 1 && notices.Count == 3,
                "pending application must resolve the current player, not retain a previous scene's instance"
            );
        }
        Console.WriteLine("PASS: Death's Door native entry, repeat protection, lifecycle and notifications");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
}
