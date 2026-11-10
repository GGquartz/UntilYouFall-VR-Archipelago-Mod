using Il2CppSG.Claymore.UI;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal sealed class VrNotificationPanel : IDisposable
{
    private readonly LogChannel _log;
    private readonly string _name;
    private readonly float _verticalPosition;
    private GameObject? _root;
    private Camera? _camera;
    private Canvas? _canvas;
    private TextMeshProUGUI? _text;
    private RectTransform? _textRect;
    private Material? _material;
    private string _skin = "Fallback";
    private string _skinSource = "None";

    public VrNotificationPanel(Action<string> log, string name, string logPrefix, float verticalPosition)
    {
        _log = log.Channel(logPrefix);
        _name = name;
        _verticalPosition = verticalPosition;
    }

    public bool IsReady =>
        _root != null
        && _camera != null
        && _camera.isActiveAndEnabled
        && _text != null
        && (_camera.cullingMask & (1 << _root.layer)) != 0;

    public bool TryCreate()
    {
        var camera = SceneCache.StereoCamera();
        if (camera == null || !camera.isActiveAndEnabled || camera.cullingMask == 0)
            return false;

        var sourceText = SceneCache.MenuFontSource();
        if (sourceText == null)
        {
            _log.Write("Waiting for pause menu font");
            return false;
        }
        var font = sourceText.font;

        var layer = WorldTextRules.FirstVisibleLayer(camera.cullingMask);
        _camera = camera;
        _root = new GameObject(_name + " Canvas") { layer = layer };
        var rect = _root.AddComponent<RectTransform>();
        rect.SetParent(camera.transform, false);
        rect.localPosition = new Vector3(0, _verticalPosition, 1.5f);
        rect.localRotation = Quaternion.identity;
        rect.localScale = new Vector3(0.001f, 0.001f, 0.001f);
        rect.sizeDelta = new Vector2(NotificationPanelLayout.PanelWidth, 70);
        _canvas = _root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.worldCamera = camera;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 30000;

        var background = _root.AddComponent<Image>();
        background.color = ApColors.NotificationBackgroundColor;
        background.raycastTarget = false;
        background.maskable = false;

        if (TryCreateForgeSkin(rect, layer))
            background.enabled = false;

        var textObject = new GameObject(_name + " Text") { layer = layer };
        var textRect = textObject.AddComponent<RectTransform>();
        _textRect = textRect;
        textRect.SetParent(rect, false);
        _text = textObject.AddComponent<TextMeshProUGUI>();
        _text.font = font;
        _text.fontStyle = sourceText.fontStyle & ~(FontStyles.UpperCase | FontStyles.LowerCase | FontStyles.SmallCaps);
        _text.fontWeight = sourceText.fontWeight;
        var sourceMaterial = sourceText.fontSharedMaterial;
        _material = new Material(sourceMaterial) { name = _name + " Font Material" };
        if (_material.HasProperty("_FaceColor"))
            _material.SetColor("_FaceColor", Color.white);
        TmpOverlayMaterial.Apply(_material);
        _text.fontSharedMaterial = _material;
        _text.color = Color.white;
        _text.richText = true;
        _text.raycastTarget = false;
        _text.maskable = false;
        _text.isOverlay = true;
        _text.alignment = TextAlignmentOptions.Center;
        _text.enableAutoSizing = false;
        _text.fontSize = 40;
        _text.enableWordWrapping = true;
        _text.overflowMode = TextOverflowModes.Overflow;
        _text.wordWrappingRatios = 0f;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(
            NotificationPanelLayout.HorizontalPadding,
            NotificationPanelLayout.VerticalPadding
        );
        textRect.offsetMax = new Vector2(
            -NotificationPanelLayout.HorizontalPadding,
            -NotificationPanelLayout.VerticalPadding
        );
        _root.SetActive(false);

        _log.Write(
            $"Ready renderer=WorldSpaceCanvas skin={_skin} skinSource={_skinSource} camera={camera.name} stereo={camera.stereoEnabled} layer={layer} font={font.name} fontSource=PauseScreen/resumeButton sourceMaterial={sourceMaterial.name} fontStyle={(int)_text.fontStyle} fontWeight={(int)_text.fontWeight} fontFallbacks={font.fallbackFontAssetTable?.Count ?? 0}"
        );
        return true;
    }

    private bool TryCreateForgeSkin(RectTransform parent, int layer)
    {
        GameObject? skin = null;
        try
        {
            var resource = Resources.Load<GameObject>(NotificationPanelLayout.NotificationResource);
            var template = resource
                ?.GetComponentsInChildren<CanvasGroup>(true)
                .FirstOrDefault(group =>
                    group != null
                    && group.gameObject.name == NotificationPanelLayout.NotificationPanelName
                    && group.transform.Find("Background") != null
                    && group.transform.Find("AccentLeft") != null
                    && group.transform.Find("AccentRight") != null
                );
            _skinSource = template != null ? NotificationPanelLayout.NotificationResource : "LoadedObjects";
            template ??= Resources
                .FindObjectsOfTypeAll<CanvasGroup>()
                .FirstOrDefault(group =>
                    group != null
                    && group.gameObject.name == NotificationPanelLayout.NotificationPanelName
                    && group.transform.Find("Background") != null
                    && group.transform.Find("AccentLeft") != null
                    && group.transform.Find("AccentRight") != null
                );
            if (template == null)
                return false;

            skin = Object.Instantiate(template.gameObject, parent);
            skin.name = _name + " Forge Notification Skin";
            SetLayer(skin, layer);

            var skinRect = skin.GetComponent<RectTransform>();
            skinRect.anchorMin = Vector2.zero;
            skinRect.anchorMax = Vector2.one;
            skinRect.pivot = new Vector2(0.5f, 0.5f);
            skinRect.anchoredPosition = Vector2.zero;
            skinRect.localPosition = Vector3.zero;
            skinRect.sizeDelta = Vector2.zero;
            skinRect.localRotation = Quaternion.identity;
            skinRect.localScale = Vector3.one;
            _log.Write($"Skin normalized localPosition={skinRect.localPosition} size={skinRect.rect.size}");

            var canvasGroup = skin.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            foreach (var text in skin.GetComponentsInChildren<TMP_Text>(true))
                text.gameObject.SetActive(false);
            var icon = skin.transform.Find("Icon");
            if (icon != null)
                icon.gameObject.SetActive(false);
            foreach (var graphic in skin.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
                if (graphic is MaskableGraphic maskable)
                    maskable.maskable = false;
            }

            ConfigureAccent(skin.transform.Find("AccentLeft"), 120f, 4f);
            ConfigureAccent(skin.transform.Find("AccentRight"), -120f, 4f);
            _skin = NotificationPanelLayout.NotificationPanelName;

            return true;
        }
        catch (Exception ex)
        {
            if (skin != null)
                Object.Destroy(skin);
            _log.Write($"Skin unavailable: {ex.Message}");
            return false;
        }
    }

    private static void ConfigureAccent(Transform? accent, float x, float scale)
    {
        if (accent is not RectTransform rect)
            return;

        rect.anchoredPosition = new Vector2(x, 0f);
        rect.localScale = new Vector3(scale, scale, scale);
    }

    private static void SetLayer(GameObject root, int layer)
    {
        root.layer = layer;
        for (var index = 0; index < root.transform.childCount; index++)
            SetLayer(root.transform.GetChild(index).gameObject, layer);
    }

    public void Show(ApNotice notice)
    {
        var rootRect = _root!.GetComponent<RectTransform>();
        var baseHeight = NotificationPanelLayout.HeightFor(0f, notice.HasLocation);
        rootRect.sizeDelta = new Vector2(NotificationPanelLayout.PanelWidth, baseHeight);
        _textRect!.anchoredPosition = Vector2.zero;
        _root!.SetActive(true);
        _text!.text = notice.RichText;
        _text.canvasRenderer.SetAlpha(1);
        Canvas.ForceUpdateCanvases();
        _text.ForceMeshUpdate();

        var required = NotificationPanelLayout.HeightFor(_text.preferredHeight, notice.HasLocation);
        if (required > baseHeight)
        {
            rootRect.sizeDelta = new Vector2(NotificationPanelLayout.PanelWidth, required);
            Canvas.ForceUpdateCanvases();
            _text.ForceMeshUpdate();
        }
        _textRect.anchoredPosition = new Vector2(
            0f,
            NotificationPanelLayout.VerticalOffsetFor(_text.textBounds.center.y)
        );
        _text.ForceMeshUpdate();
    }

    public void Hide()
    {
        if (_root != null)
            _root.SetActive(false);
    }

    public void LogRenderState()
    {
        if (!IsReady)
            return;
        var position = _camera!.WorldToViewportPoint(_text!.transform.position);
        var rect = _text.rectTransform.rect;
        var layer = _text.gameObject.layer;
        var vertices = _text.mesh != null ? _text.mesh.vertexCount : 0;

        _log.Write(
            $"RenderState active={_root!.activeInHierarchy} skin={_skin} skinSource={_skinSource} camera={_camera.name} stereo={_camera.stereoEnabled} layer={layer} inCameraMask={(_camera.cullingMask & (1 << layer)) != 0} viewport={position} scale={_root.transform.lossyScale} rect={rect.width:F1}x{rect.height:F1} verticalOffset={_textRect!.anchoredPosition.y:F1} alpha={_text.color.a:F2} rendererAlpha={_text.canvasRenderer.GetAlpha():F2} culled={_text.canvasRenderer.cull} characters={_text.textInfo.characterCount} vertices={vertices} canvas={_canvas!.renderMode} shader={_material!.shader.name}"
        );
    }

    public void Dispose()
    {
        if (_root != null)
            Object.Destroy(_root);
        if (_material != null)
            Object.Destroy(_material);
        _root = null;
        _camera = null;
        _canvas = null;
        _text = null;
        _textRect = null;
        _material = null;
        _skin = "Fallback";
        _skinSource = "None";
    }
}
