using HarmonyLib;
using Il2CppSG.Claymore.UI;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UntilYouFallArchipelago;

internal sealed class ConnectionButtons
{
    private const string ConnectName = "AP Connect";
    private const string DisconnectName = "AP Disconnect";
    private const string ConnectLabel = "Connect";
    private const string DisconnectLabel = "Disconnect";
    private const string QuitAndCreditsName = "QuitAndCredits";
    private const string ReturnButtonName = "ButtonReturn";
    private const string UserStringComponentName = "UserStringComponent";

    private const float ProfileArcRadius = 4250f;
    private const float ProfileColumnX = 1835f;
    private const float ProfileButtonSpacing = 140f;
    private static readonly Vector2 ProfileButtonSize = new(900f, 240f);

    private const float MinLookDirectionSqrMagnitude = 1e-6f;
    private const float PauseRowY = 40f;
    private const float PauseRaiseMeters = 2f;
    private const float PauseExtraTiltDownDegrees = 10f;
    private const float PauseButtonSpacing = 185f;
    private static readonly Vector2 PauseButtonSize = new(330f, 100f);

    private static ConnectionButtons _instance = null!;
    private readonly List<RectTransform> _pauseButtons = [];
    private readonly Action _connect;
    private readonly Action _disconnect;
    private readonly LogChannel _log;

    public ConnectionButtons(HarmonyLib.Harmony harmony, Action connect, Action disconnect, Action<string> log)
    {
        _instance = this;
        _connect = connect;
        _disconnect = disconnect;
        _log = log.Channel("APConnectButtons");
        harmony.PatchAll(typeof(ConnectionButtons));
    }

    [HarmonyPatch(typeof(ProfileUI), "OnEnable")]
    [HarmonyPatch(typeof(ProfileUI), nameof(ProfileUI.AttemptLoad))]
    [HarmonyPostfix]
    private static void AfterProfileShown(ProfileUI __instance) =>
        _instance.Guard(() => _instance.AddProfileButtons(__instance));

    [HarmonyPatch(typeof(PauseScreen), "OnEnable")]
    [HarmonyPostfix]
    private static void AfterPauseShown(PauseScreen __instance) =>
        _instance.Guard(() => _instance.AddPauseButtons(__instance));

    public void Update()
    {
        if (_pauseButtons.Count == 0 || _pauseButtons[0] == null || !_pauseButtons[0].gameObject.activeInHierarchy)
            return;

        Guard(OrientPauseButtons);
    }

    private void OrientPauseButtons()
    {
        var head = Camera.main;
        _pauseButtons.RemoveAll(button => button == null);
        if (head == null)
            return;

        foreach (var button in _pauseButtons)
        {
            var direction = button.parent.InverseTransformDirection(button.position - head.transform.position);
            if (direction.sqrMagnitude <= MinLookDirectionSqrMagnitude)
                continue;

            button.localRotation =
                Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(-PauseExtraTiltDownDegrees, 0f, 0f);
        }
    }

    private void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _log.Write($"Failed to add connection buttons: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void AddProfileButtons(ProfileUI profileUi)
    {
        var slots = profileUi.transform.parent;
        var row = slots?.parent?.Find(QuitAndCreditsName);
        if (slots == null || row == null || slots.Find(ConnectName) != null)
            return;

        var returnButton = row.Find(ReturnButtonName);
        if (returnButton == null)
        {
            _log.Write("Return button was not found");
            return;
        }

        var angle = Mathf.Asin(ProfileColumnX / ProfileArcRadius);
        var depth = -ProfileArcRadius * (1f - Mathf.Cos(angle));
        var yaw = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
        var connect = Clone(returnButton, slots, ConnectName, ConnectLabel, _connect);
        var disconnect = Clone(returnButton, slots, DisconnectName, DisconnectLabel, _disconnect);

        PlaceOnArc(connect, new Vector3(ProfileColumnX, ProfileButtonSpacing, depth), yaw);
        PlaceOnArc(disconnect, new Vector3(ProfileColumnX, -ProfileButtonSpacing, depth), yaw);
    }

    private static void PlaceOnArc(RectTransform rect, Vector3 position, Quaternion rotation)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = ProfileButtonSize;
        rect.anchoredPosition3D = position;
        rect.localRotation = rotation;
    }

    private void AddPauseButtons(PauseScreen pause)
    {
        var resume = pause.resumeButton;
        var parent = resume?.transform.parent;
        if (resume == null || parent == null)
            return;

        if (parent.Find(ConnectName) != null)
            return;

        var connect = Clone(resume.transform, parent, ConnectName, ConnectLabel, _connect);
        var disconnect = Clone(resume.transform, parent, DisconnectName, DisconnectLabel, _disconnect);
        connect.sizeDelta = disconnect.sizeDelta = PauseButtonSize;
        var metersPerUnit = Mathf.Abs(connect.lossyScale.y);
        var rowY = metersPerUnit > 1e-6f ? PauseRowY + PauseRaiseMeters / metersPerUnit : PauseRowY;
        connect.anchoredPosition = new Vector2(-PauseButtonSpacing, rowY);
        disconnect.anchoredPosition = new Vector2(PauseButtonSpacing, rowY);

        _pauseButtons.Clear();
        _pauseButtons.Add(connect);
        _pauseButtons.Add(disconnect);
    }

    private RectTransform Clone(Transform template, Transform parent, string name, string label, Action click)
    {
        var clone = UnityEngine.Object.Instantiate(template.gameObject, parent);
        clone.name = name;
        var rect = clone.GetComponent<RectTransform>();

        var button = clone.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(
            (UnityAction)(
                () =>
                {
                    try
                    {
                        click();
                    }
                    catch (Exception ex)
                    {
                        _log.Write($"{name} click failed: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            )
        );

        foreach (var component in clone.GetComponentsInChildren<Component>(true))
            if (component != null && component.ToString().Contains(UserStringComponentName, StringComparison.Ordinal))
                UnityEngine.Object.Destroy(component);

        var text = clone.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault();
        if (text != null)
            text.text = label;

        return rect;
    }
}
