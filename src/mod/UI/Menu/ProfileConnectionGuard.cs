using HarmonyLib;
using Il2CppSG.Claymore.UI;

namespace UntilYouFallArchipelago;

internal sealed class ProfileConnectionGuard
{
    private const string OfflineText = "Please connect to the AP server";
    private static readonly ApNotice OfflineNotice = ApNotice.Error("Please connect to the AP server first");
    private static ProfileConnectionGuard _instance = null!;
    private readonly Func<bool> _isConnected;
    private readonly Action<ApNotice> _showNotice;
    private readonly Action _menuEntered;
    private readonly LogChannel _log;
    private readonly List<ProfileSlot> _slots = [];
    private bool? _connected;

    public ProfileConnectionGuard(
        HarmonyLib.Harmony harmony,
        Func<bool> isConnected,
        Action<ApNotice> showNotice,
        Action menuEntered,
        Action<string> log
    )
    {
        _instance = this;
        _isConnected = isConnected;
        _showNotice = showNotice;
        _menuEntered = menuEntered;
        _log = log.Channel("APProfileConnection");
        harmony.PatchAll(typeof(ProfileConnectionGuard));
    }

    public void Update()
    {
        var connected = _isConnected();
        if (_connected == connected)
            return;

        _slots.RemoveAll(slot => slot.Profile == null);
        foreach (var slot in _slots)
            Refresh(slot.Profile);
        _connected = connected;

        _log.Write($"Profile entry connected={connected} slots={_slots.Count}");
    }

    [HarmonyPatch(typeof(ProfileUI), "OnEnable")]
    [HarmonyPostfix]
    private static void AfterEnable(ProfileUI __instance)
    {
        _instance._menuEntered();
        _instance.Refresh(__instance);
    }

    [HarmonyPatch(typeof(ProfileUI), nameof(ProfileUI.AttemptLoad))]
    [HarmonyPatch(typeof(ProfileUI), nameof(ProfileUI.ShowNewGameMode))]
    [HarmonyPostfix]
    private static void AfterRefresh(ProfileUI __instance) => _instance.Refresh(__instance);

    [HarmonyPatch(typeof(PressAndHoldButton), nameof(PressAndHoldButton.OnPointerDown))]
    [HarmonyPostfix]
    private static void AfterButtonPress(PressAndHoldButton __instance) => _instance.ExplainOfflineSave(__instance);

    [HarmonyPatch(typeof(ProfileUI), nameof(ProfileUI.LoadProfile))]
    [HarmonyPatch(typeof(ProfileUI), nameof(ProfileUI.LoadData))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool BeforeLoad(ProfileUI __instance)
    {
        if (_instance._isConnected())
            return true;

        _instance.Refresh(__instance);

        _instance._log.Write($"Blocked profile entry profile={__instance._profileIndex} new={__instance._isNewGame}");
        return false;
    }

    private void ExplainOfflineSave(PressAndHoldButton button)
    {
        if (_isConnected())
            return;

        var profile = _slots.Select(slot => slot.Profile).FirstOrDefault(profile => profile?.playButton == button);
        if (profile == null || profile._isNewGame)
            return;

        _showNotice(OfflineNotice);
        _log.Write($"Offline save press profile={profile._profileIndex}");
    }

    private void Refresh(ProfileUI profile)
    {
        try
        {
            _slots.RemoveAll(slot => slot.Profile == null);
            var slot = _slots.FirstOrDefault(slot => slot.Profile == profile);
            if (slot == null)
            {
                slot = new ProfileSlot(profile);
                _slots.Add(slot);
            }
            slot.Refresh(_isConnected());
        }
        catch (Exception ex)
        {
            _log.Write($"Profile prompt failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private sealed class ProfileSlot(ProfileUI profile)
    {
        public ProfileUI Profile { get; } = profile;
        private string _newGameText = "";
        private bool _blocked;
        private bool _wasInteractable;
        private bool _wasAutoSizing;
        private float _fontSize;
        private float _maxFontSize;

        public void Refresh(bool connected)
        {
            var newGameText = Profile._newGameTextMeshPro;
            var button = Profile.playButton;
            if (newGameText != null && newGameText.text != OfflineText)
                _newGameText = newGameText.text;

            if (connected)
            {
                if (!_blocked)
                    return;

                if (newGameText != null)
                {
                    newGameText.text = _newGameText;
                    newGameText.enableAutoSizing = _wasAutoSizing;
                    newGameText.fontSizeMax = _maxFontSize;
                    newGameText.fontSize = _fontSize;
                    newGameText.gameObject.SetActive(Profile._isNewGame);
                }
                if (button != null)
                    button.IsInteractable = _wasInteractable;

                _blocked = false;
                return;
            }

            if (!_blocked)
            {
                _wasInteractable = button != null && button.IsInteractable;
                if (newGameText != null)
                {
                    _wasAutoSizing = newGameText.enableAutoSizing;
                    _fontSize = newGameText.fontSize;
                    _maxFontSize = newGameText.fontSizeMax;
                }
            }
            _blocked = true;
            if (newGameText != null)
            {
                newGameText.text = OfflineText;
                newGameText.fontSizeMax = _fontSize;
                newGameText.enableAutoSizing = true;
                newGameText.gameObject.SetActive(true);
            }
            if (button != null && button.IsInteractable)
            {
                button.IsInteractable = false;
                button.AbortFill();
            }
        }
    }
}
