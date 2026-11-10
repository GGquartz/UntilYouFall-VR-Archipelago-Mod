using Il2CppSG.Claymore.Interaction;
using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.SaveSystem;
using Il2CppSG.Claymore.UI;
using Il2CppSG.XR;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal sealed class ApWristConsole : IDisposable
{
    private readonly LogChannel _log;
    private readonly LogChannel _consoleLog;
    private readonly ApConsoleBuffer _buffer = new();
    private GameObject? _root;
    private Camera? _camera;
    private ArmamentInventory? _inventory;
    private PlayerHand? _hand;
    private XRPlatformManager? _xr;
    private TextMeshProUGUI? _text;
    private Material? _material;
    private Material? _backgroundMaterial;
    private WristConsoleAnchor? _anchor;
    private long _retryAt;
    private long _nextTrackingSearchAt;
    private bool _dirty = true;
    private bool _combatHidden;
    private bool _shown;

    private readonly Func<LevelManager?> _level;

    public ApWristConsole(Func<LevelManager?> level, Action<string> log)
    {
        _level = level;
        _log = log.Channel("APConsoleUI");
        _consoleLog = log.Channel("APConsole");
    }

    public void Add(ApConsoleMessage message)
    {
        _buffer.Add(message);
        _dirty = true;
        _consoleLog.Write("" + message.PlainText);
    }

    public void SetCombatHidden(bool hidden)
    {
        if (_combatHidden == hidden)
            return;
        _combatHidden = hidden;
        _log.Write($"Combat visibility: hidden={hidden}");

        if (hidden)
        {
            if (_root != null)
                _root.SetActive(false);
            _shown = false;
        }
        else
        {
            _dirty = true;
            _retryAt = 0;
            _log.Write(
                $"Restore after combat handEnabled={_hand != null && _hand.isActiveAndEnabled}; handActive={_hand != null && _hand.gameObject.activeInHierarchy}; cameraActive={_camera != null && _camera.isActiveAndEnabled}"
            );
        }
    }

    public void Update()
    {
        try
        {
            if (_combatHidden)
                return;
            if (_inventory != null && (_hand == null || !_hand.isActiveAndEnabled))
                _hand = SelectHand(_inventory);
            RefreshTracking();
            if (_root == null || _camera == null || !_camera.isActiveAndEnabled)
            {
                if (Environment.TickCount64 < _retryAt)
                    return;
                DisposePanel();
                _retryAt = Environment.TickCount64 + 5000;
                if (!TryCreate())
                    return;
            }

            var handPosition = HandPosition(_camera!.transform);
            var pose = _anchor!.GetPose(handPosition, _camera!.transform);
            _root!.transform.SetPositionAndRotation(pose.position, pose.rotation);
            if (!_shown)
            {
                _root.SetActive(true);
                _shown = true;
            }

            if (_dirty || _text!.havePropertiesChanged)
            {
                Canvas.ForceUpdateCanvases();
                var content = _buffer.RichText;
                _text!.text = string.IsNullOrEmpty(content) ? "Waiting for Archipelago..." : content;
                _text.ForceMeshUpdate();
                _dirty = false;
                TmpTextBounds.Clip(_text);
            }
        }
        catch (Exception ex)
        {
            _log.Write("Retrying: " + ex);
            Dispose();
            _retryAt = Environment.TickCount64 + 10000;
        }
    }

    private void RefreshTracking()
    {
        if (_xr != null)
            return;
        var now = Environment.TickCount64;
        if (now < _nextTrackingSearchAt)
            return;

        _nextTrackingSearchAt = now + 1000;
        SceneCache.Find(ref _xr);
        if (_xr != null)
            _log.Write($"Tracking source acquired manager={_xr.GetInstanceID()}");
    }

    private Vector3 HandPosition(Transform head)
    {
        var handTransform = _hand != null ? _hand.transform.position : head.position + head.forward * 0.5f;
        Vector3? tracked = null;
        var node = _xr?.LeftHandNode;
        if (node != null)
        {
            var position = node.WorldPose.position;
            if (position.sqrMagnitude > 0.0001f && (position - head.position).magnitude < 1.5f)
                tracked = position;
        }

        return tracked ?? handTransform;
    }

    private bool TryCreate()
    {
        var camera = SceneCache.StereoCamera();
        if (camera == null || !camera.isActiveAndEnabled || camera.cullingMask == 0)
            return false;
        var inventory = FindInventory();
        var hand = SelectHand(inventory);
        var sourceText = SceneCache.MenuFontSource();
        if (sourceText == null)
            return false;
        if (_xr == null && hand == null)
            return false;
        var font = sourceText.font;

        _camera = camera;
        _inventory = inventory;
        _hand = hand;
        _anchor = new WristConsoleAnchor();
        var layer = WorldTextRules.FirstVisibleLayer(camera.cullingMask);
        _root = new GameObject("Archipelago Wrist Console") { layer = layer };
        var rect = _root.AddComponent<RectTransform>();
        rect.localScale = new Vector3(0.00025f, 0.00025f, 0.00025f);
        rect.sizeDelta = new Vector2(640, 400);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 29999;
        var background = _root.AddComponent<Image>();
        background.color = ApColors.WristConsoleBackgroundColor;
        background.raycastTarget = false;
        background.maskable = false;
        _backgroundMaterial = new Material(background.material) { name = "Archipelago Console Background Material" };
        _backgroundMaterial.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
        background.material = _backgroundMaterial;

        var textObject = new GameObject("Archipelago Console Text") { layer = layer };
        var textRect = textObject.AddComponent<RectTransform>();
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18, 16);
        textRect.offsetMax = new Vector2(-18, -16);
        _text = textObject.AddComponent<TextMeshProUGUI>();
        _text.font = font;
        _text.fontStyle = sourceText.fontStyle & ~(FontStyles.UpperCase | FontStyles.LowerCase | FontStyles.SmallCaps);
        _text.fontWeight = sourceText.fontWeight;
        _material = new Material(sourceText.fontSharedMaterial) { name = "Archipelago Console Font Material" };
        if (_material.HasProperty("_FaceColor"))
            _material.SetColor("_FaceColor", Color.white);
        TmpOverlayMaterial.Apply(_material);
        if (_material.HasProperty("_VertexOffsetX"))
            _material.SetFloat("_VertexOffsetX", 0);
        if (_material.HasProperty("_VertexOffsetY"))
            _material.SetFloat("_VertexOffsetY", 0);
        _text.fontSharedMaterial = _material;
        _text.color = Color.white;
        _text.richText = true;
        _text.raycastTarget = false;
        _text.maskable = false;
        _text.isOverlay = true;
        _text.alignment = TextAlignmentOptions.BottomLeft;
        _text.enableAutoSizing = false;
        _text.fontSize = 24;
        _text.enableWordWrapping = true;
        _text.overflowMode = TextOverflowModes.Overflow;
        rect.localScale = new Vector3(0.00025f, 0.00025f, 0.00025f);
        rect.sizeDelta = new Vector2(640, 400);
        textRect.localRotation = Quaternion.identity;
        textRect.localScale = Vector3.one;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = new Vector2(-36, -32);
        textRect.anchoredPosition3D = Vector3.zero;
        _dirty = true;
        _root.SetActive(false);

        _log.Write(
            $"Ready hand={(hand != null ? hand.name : "none")} camera={camera.name} layer={layer} font={font.name} fontSource=PauseScreen/resumeButton material={sourceText.fontSharedMaterial.name}"
        );
        return true;
    }

    private ArmamentInventory? FindInventory()
    {
        var live = _level()?.armamentInventory;
        if (live?.saveManager != null && SelectHand(live) != null)
            return live;

        ArmamentInventory? fallback = null;
        foreach (var inventory in Resources.FindObjectsOfTypeAll<ArmamentInventory>())
        {
            try
            {
                if (inventory.saveManager == null)
                    continue;
                var hand = SelectHand(inventory);
                if (hand == null)
                    continue;
                if (hand.gameObject.activeInHierarchy)
                    return inventory;
                fallback ??= inventory;
            }
            catch (Exception ex)
            {
                _log.Write($"Skipping unusable inventory {inventory.GetInstanceID()}: {ex.GetType().Name}");
            }
        }

        return fallback;
    }

    private static PlayerHand? SelectHand(ArmamentInventory? inventory) => inventory?.leftHand?.PlayerHand;

    private void DisposePanel()
    {
        if (_root != null)
            Object.Destroy(_root);
        if (_material != null)
            Object.Destroy(_material);
        if (_backgroundMaterial != null)
            Object.Destroy(_backgroundMaterial);
        _root = null;
        _shown = false;
        _camera = null;
        _inventory = null;
        _hand = null;
        _text = null;
        _material = null;
        _backgroundMaterial = null;
        _anchor = null;
    }

    public void Dispose()
    {
        DisposePanel();
        _xr = null;
        _nextTrackingSearchAt = 0;
    }
}
