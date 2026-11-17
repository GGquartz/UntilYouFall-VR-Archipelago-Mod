using System.Reflection;
using Il2CppSG.Claymore.UI;
using UntilYouFallArchipelago;

internal static class ProfileConnectionGuardTests
{
    public static void Run()
    {
        var connected = false;
        var notices = new List<ApNotice>();
        var menuEntries = 0;
        var guard = new ProfileConnectionGuard(
            new HarmonyLib.Harmony(),
            () => connected,
            notices.Add,
            () => menuEntries++,
            _ => { }
        );
        var guardedMethods = Handler("BeforeLoad")
            .GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType == typeof(HarmonyLib.HarmonyPatch))
            .Select(attribute => (string)attribute.ConstructorArguments[1].Value!)
            .ToHashSet();
        Check(
            guardedMethods.SetEquals(["LoadData", "LoadProfile"]),
            "both the pre-delay and post-delay native button actions are guarded"
        );
        var existing = new ProfileUI();
        var fresh = new ProfileUI { _isNewGame = true };
        fresh._newGameTextMeshPro.gameObject.SetActive(true);
        var disabled = new ProfileUI();
        var cleared = new ProfileUI();
        disabled.playButton.IsInteractable = false;
        Refresh(existing);
        Refresh(fresh);
        Refresh(disabled);
        Refresh(cleared);
        foreach (var profile in new[] { existing, fresh, disabled })
        {
            Check(profile._newGameTextMeshPro.text == "Please connect to the AP server", "new save text is blocked");
            Check(
                profile._newGameTextMeshPro.gameObject.activeSelf,
                "the prompt is visible even for an existing save with no room label"
            );
            Check(profile._newGameTextMeshPro.enableAutoSizing, "the longer prompt fits the native new-game text area");
            Check(!profile.playButton.IsInteractable && !CanLoad(profile), "offline slots cannot enter a save");
        }
        Check(existing.playButton.AbortCount == 1, "blocking aborts an active hold only once");
        Check(notices.Count == 0, "opening the menu offline shows no technical notice");
        Press(fresh);
        Press(new ProfileUI().playButton);
        Check(notices.Count == 0, "empty and unknown slots keep their own prompt only");
        Press(existing);
        Check(
            notices.Count == 1 && notices[0].PlainText == "Please connect to the AP server first",
            "pressing an existing offline save explains the block with a technical notice"
        );
        Enable(existing);
        Check(menuEntries == 1, "opening the profile menu dismisses the disconnect warning");
        guard.Update();
        guard.Update();
        Check(existing.playButton.AbortCount == 1, "unchanged connection does not keep rewriting slot state");

        existing._newGameTextMeshPro.gameObject.SetActive(false);
        Refresh(existing);
        fresh._newGameTextMeshPro.text = "Start New Game";
        Refresh(fresh);
        Check(existing._newGameTextMeshPro.gameObject.activeSelf, "native preview redraw stays blocked");
        Check(
            fresh._newGameTextMeshPro.text == "Please connect to the AP server",
            "new-game mode redraw stays blocked"
        );

        connected = true;
        Press(existing);
        Check(notices.Count == 1, "a connected press shows no offline notice");
        guard.Update();
        Check(
            !existing._newGameTextMeshPro.gameObject.activeSelf,
            "reconnect hides the new-game prompt for an existing save"
        );
        Check(
            !existing._newGameTextMeshPro.enableAutoSizing
                && existing._newGameTextMeshPro.fontSize == 110
                && existing._newGameTextMeshPro.fontSizeMax == 72,
            "reconnect restores native font settings"
        );
        Check(fresh._newGameTextMeshPro.text == "Start New Game", "reconnect restores the latest native new-save text");
        Check(existing.playButton.IsInteractable && fresh.playButton.IsInteractable, "connected slots become usable");
        Check(!disabled.playButton.IsInteractable, "a natively disabled button is not incorrectly enabled");
        Check(CanLoad(existing) && CanLoad(fresh), "connected new and existing profile entry is allowed");

        connected = false;
        Check(!CanLoad(existing), "a delayed activation is blocked before the next UI poll");
        Check(!CanLoad(fresh), "a new save is also blocked before the next UI poll");
        guard.Update();
        cleared._isNewGame = true;
        cleared._newGameTextMeshPro.gameObject.SetActive(true);
        Refresh(cleared);
        Check(
            !existing.playButton.IsInteractable && !fresh.playButton.IsInteractable,
            "disconnect disables both kinds of slots"
        );
        Check(existing.playButton.AbortCount == 2, "each disconnect cancels held input once");
        guard.Update();
        Check(!CanLoad(existing), "a failed reconnect remains blocked");

        connected = true;
        existing._newGameTextMeshPro.text = "Localized New Game";
        Refresh(existing);
        Check(
            existing._newGameTextMeshPro.text == "Localized New Game",
            "a native refresh during reconnect is not overwritten by an old snapshot"
        );
        guard.Update();
        Check(fresh._newGameTextMeshPro.text == "Start New Game", "other tracked slots recover on successful login");
        Check(
            cleared._newGameTextMeshPro.gameObject.activeSelf && cleared.playButton.IsInteractable,
            "a slot cleared while offline returns to the native new-save presentation"
        );
        Console.WriteLine(
            "PASS: offline profile entry, native text restoration, held/delayed activation and reconnect"
        );
    }

    private static void Refresh(ProfileUI profile) => Handler("AfterRefresh").Invoke(null, [profile]);

    private static void Press(ProfileUI profile) => Press(profile.playButton);

    private static void Press(PressAndHoldButton button) =>
        typeof(ProfileConnectionGuard)
            .GetMethod("AfterButtonPress", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [button]);

    private static void Enable(ProfileUI profile) => Handler("AfterEnable").Invoke(null, [profile]);

    private static bool CanLoad(ProfileUI profile) => (bool)Handler("BeforeLoad").Invoke(null, [profile])!;

    private static MethodInfo Handler(string name) =>
        typeof(ProfileConnectionGuard).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception("Profile connection guard: " + message);
    }
}
