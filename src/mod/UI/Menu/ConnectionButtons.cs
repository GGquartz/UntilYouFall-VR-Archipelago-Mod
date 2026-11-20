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

    private const string PauseRootName = "AP Connection Buttons";
    private const float MinLookDirectionSqrMagnitude = 1e-6f;
    private const float PauseRefreshSeconds = 0.25f;
    private const float PauseRowY = 40f;
    private const float PauseRaiseMeters = 2f;
    private const float PauseButtonSpacing = 185f;
    private static readonly Vector2 PauseButtonSize = new(330f, 100f);

    private static ConnectionButtons _instance = null!;
    private RectTransform? _pauseRoot;
    private float _nextPauseRefresh;
    private bool _needsPauseOrient;
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
        if (!_needsPauseOrient || _pauseRoot == null || !_pauseRoot.gameObject.activeInHierarchy)
            return;

        var now = Time.unscaledTime;
        if (now < _nextPauseRefresh)
            return;

        _nextPauseRefresh = now + PauseRefreshSeconds;
        Guard(OrientPauseButtons);
    }

    private void OrientPauseButtons()
    {
        var head = Camera.main;
        if (head == null || _pauseRoot == null)
            return;

        var direction = _pauseRoot.parent.InverseTransformDirection(_pauseRoot.position - head.transform.position);
        if (direction.sqrMagnitude <= MinLookDirectionSqrMagnitude)
            return;

        _pauseRoot.localRotation = Quaternion.LookRotation(direction, Vector3.up);
        _needsPauseOrient = false;
    }

    private void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _log.Write($"Connection buttons failed: {ex.GetType().Name}: {ex.Message}");
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

        _needsPauseOrient = true;
        _nextPauseRefresh = 0f;
        if (_pauseRoot != null)
            return;

        var resumeRect = resume.GetComponent<RectTransform>();
        var metersPerUnit = Mathf.Abs(resumeRect.lossyScale.y);
        var rowY = metersPerUnit > 1e-6f ? PauseRowY + PauseRaiseMeters / metersPerUnit : PauseRowY;
        var root = new GameObject(PauseRootName) { layer = parent.gameObject.layer };
        _pauseRoot = root.AddComponent<RectTransform>();
        _pauseRoot.SetParent(parent, false);
        root.AddComponent<LayoutElement>().ignoreLayout = true;
        PlaceLike(_pauseRoot, resumeRect, new Vector2(0f, rowY), PauseButtonSize);
        _pauseRoot.SetSiblingIndex(resume.transform.GetSiblingIndex() + 1);

        var connect = Clone(resume.transform, _pauseRoot, ConnectName, ConnectLabel, _connect);
        var disconnect = Clone(resume.transform, _pauseRoot, DisconnectName, DisconnectLabel, _disconnect);
        PlaceLike(connect, resumeRect, new Vector2(-PauseButtonSpacing, 0f), PauseButtonSize);
        PlaceLike(disconnect, resumeRect, new Vector2(PauseButtonSpacing, 0f), PauseButtonSize);
    }

    private static void PlaceLike(RectTransform rect, RectTransform reference, Vector2 position, Vector2 size)
    {
        rect.anchorMin = reference.anchorMin;
        rect.anchorMax = reference.anchorMax;
        rect.pivot = reference.pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localRotation = reference.localRotation;
        rect.localScale = reference.localScale;
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
