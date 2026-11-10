using HarmonyLib;
using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.SaveSystem.SavedElements;
using Il2CppSG.Claymore.UI.Popup;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal sealed class TutorialSkip
{
    private static TutorialSkip _instance = null!;
    private readonly Func<bool> _connected;
    private readonly LogChannel _log;
    private readonly HashSet<string> _loggedPopups = [];
    private int _lastProfile = -1;
    private IntPtr _checkedRoom = IntPtr.Zero;
    private bool _failed;

    public TutorialSkip(HarmonyLib.Harmony harmony, Func<bool> connected, Action<string> log)
    {
        _instance = this;
        _connected = connected;
        _log = log.Channel("APTutorial");
        harmony.Patch(
            AccessTools.Method(typeof(LevelManager), nameof(LevelManager.StartGame)),
            prefix: new HarmonyMethod(typeof(TutorialSkip), nameof(BeforeStartGame))
        );
        harmony.Patch(
            AccessTools.Method(typeof(LevelManager), nameof(LevelManager.StartRun)),
            prefix: new HarmonyMethod(typeof(TutorialSkip), nameof(BeforeStartGame))
        );
        harmony.Patch(
            AccessTools.Method(typeof(PopupUIController), nameof(PopupUIController.OnShowPopupUI)),
            prefix: new HarmonyMethod(typeof(TutorialSkip), nameof(AllowPopup))
        );
        harmony.Patch(
            AccessTools.Method(typeof(PopupUIRandomDisplay), nameof(PopupUIRandomDisplay.Display)),
            prefix: new HarmonyMethod(typeof(TutorialSkip), nameof(AllowBattleTip))
        );
        harmony.Patch(
            AccessTools.Method(typeof(PopupUIRandomDisplay), "Update"),
            prefix: new HarmonyMethod(typeof(TutorialSkip), nameof(AllowBattleTip))
        );
    }

    private static bool AllowPopup(PopupUIAsset __0)
    {
        if (!_instance._connected() || __0 == null || !TutorialRules.IsTutorialPopup(__0.name))
            return true;
        if (_instance._loggedPopups.Add(__0.name))
            _instance._log.Write("Suppressed popup: " + __0.name);

        return false;
    }

    private static bool AllowBattleTip(PopupUIRandomDisplay __instance)
    {
        if (!_instance._connected())
            return true;
        var name = __instance?.gameObject?.name;
        if (!IsBattleTipDisplay(__instance, name))
            return true;
        __instance!.displayed = true;
        if (_instance._loggedPopups.Add(name!))
            _instance._log.Write("Suppressed battle tip: " + name);

        return false;
    }

    private static bool IsBattleTipDisplay(PopupUIRandomDisplay? display, string? objectName)
    {
        if (TutorialRules.IsBattleTip(objectName))
            return true;
        var choices = display?.choices;
        if (choices == null)
            return false;
        foreach (var choice in choices)
            if (choice != null && TutorialRules.IsBattleTip(choice.name))
                return true;

        return false;
    }

    private static void BeforeStartGame(LevelManager __instance)
    {
        if (!_instance._connected() || _instance._failed)
            return;
        try
        {
            _instance.PrepareProfile(__instance);
        }
        catch (Exception ex)
        {
            _instance.Fail(ex);
        }
    }

    public void Update(LevelManager? manager)
    {
        if (!_connected())
        {
            _lastProfile = -1;
            _checkedRoom = IntPtr.Zero;
            _failed = false;
            return;
        }
        if (_failed || manager == null)
            return;
        if (manager.IsRoomLoading())
        {
            _checkedRoom = IntPtr.Zero;
            return;
        }
        var room = manager.GameplayRoom();
        if (room == null)
            return;
        if (room.Pointer == _checkedRoom && manager.saveManager?.ProfileIndex == _lastProfile)
            return;
        try
        {
            if (!PrepareProfile(manager))
                return;
            if (
                TutorialRules.ShouldReturnToRuneforge(
                    _connected(),
                    false,
                    manager.Campaign?.GUID,
                    manager.tutorialCampaign?.GUID,
                    room.GUID
                )
            )
            {
                manager.Campaign = manager.DefaultCampaign;
                _log.Write($"Loading runeforge profile={manager.saveManager?.ProfileIndex} skippedRoom={room.GUID}");
                manager.LoadHomeScene();
                return;
            }
            _checkedRoom = room.Pointer;
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    private bool PrepareProfile(LevelManager manager)
    {
        var save = manager.saveManager;
        if (
            !save.HasProfile()
            || save.UserDataHandler == null
            || manager.tutorialCompleted == null
            || manager.DefaultCampaign == null
        )
            return false;
        var changed = 0;
        if (!manager.tutorialCompleted.Value)
        {
            manager.tutorialCompleted.Value = true;
            changed++;
        }
        foreach (var flag in manager.tutorialTrackVariable)
        {
            if (flag == null || flag.Value)
                continue;
            flag.Value = true;
            changed++;
        }
        foreach (var element in save.tutorial.playerSettings)
        {
            var setting = element.TryCast<SavedBool>();
            var flag = setting?.variable;
            if (flag == null || flag.name != "FTUE_TwoHandedWeapon_FirstTimeUse" || flag.Value)
                continue;
            flag.Value = true;
            changed++;
        }
        var changedRooms = TutorialRooms.MarkVisited(manager.DefaultCampaign);
        if (changed > 0 || changedRooms > 0)
            save.SaveTutorial();
        if (changed > 0 || changedRooms > 0 || _lastProfile != save.ProfileIndex)
        {
            DismissTutorialPopups();
            _log.Write(
                $"Profile prepared profile={save.ProfileIndex} changedFlags={changed} changedRooms={changedRooms} tutorialCompleted={manager.tutorialCompleted.Value}"
            );
            _lastProfile = save.ProfileIndex;
        }

        return true;
    }

    private static void DismissTutorialPopups()
    {
        foreach (var controller in Object.FindObjectsOfType<PopupUIController>())
        {
            var popups = controller._activePopupUIDictionary;
            if (popups == null)
                continue;
            var pending = new List<PopupUIAsset>();
            foreach (var asset in popups.Keys)
                if (asset != null && TutorialRules.IsTutorialPopup(asset.name))
                    pending.Add(asset);
            foreach (var asset in pending)
                controller.OnDismissPopupUI(asset);
        }
    }

    private void Fail(Exception ex)
    {
        _failed = true;
        _log.Write("Skip failed" + ex);
    }
}
