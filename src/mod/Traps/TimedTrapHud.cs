using Il2CppSG.Claymore.UI;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal sealed class TimedTrapHud : IDisposable
{
    private readonly LogChannel _log;
    private readonly LogChannel _noBlockHudLog;
    private readonly string _title;
    private readonly Func<int> _row;
    private readonly Color _color;
    private PlayerUI? _ui;
    private GameObject? _root;
    private TextMeshProUGUI? _text;
    private RectTransform? _fill;
    private CanvasGroup? _group;
    private Material? _material;
    private long _retryAt;

    public TimedTrapHud(string title, Func<int> row, Action<string> log, Color? color = null)
    {
        _title = title;
        _row = row;
        _log = log.Channel("APTrapHud");
        _noBlockHudLog = log.Channel("APNoBlockHud");
        _color = color ?? ApColors.TimedTrapDefaultColor;
    }

    private int _shownSeconds = -1;

    public void Update(double remainingSeconds, int durationSeconds)
    {
        try
        {
            if (_root == null || _ui == null || !_ui.isActiveAndEnabled)
            {
                if (Environment.TickCount64 < _retryAt)
                    return;
                _retryAt = Environment.TickCount64 + 1000;
                if (!TryCreate())
                    return;
            }

            var seconds = (int)Math.Ceiling(remainingSeconds);
            if (seconds != _shownSeconds || _text!.text.Length == 0)
            {
                _shownSeconds = seconds;
                _text!.text = $"{_title} - {seconds / 60}:{seconds % 60:00}";
            }
            _fill!.anchorMax = new Vector2((float)(remainingSeconds / durationSeconds), 1);
            _group!.alpha = _ui!._currentAlpha;
            _root!.transform.localPosition = new Vector3(0, TimedTrapHudLayout.Height(_row()), -0.05f);
            _root!.transform.rotation = _ui!._dashDamageText.transform.rotation;
            _root!.SetActive(true);
        }
        catch (Exception exception)
        {
            Dispose();
            _retryAt = Environment.TickCount64 + 1000;
            _log.Write($"{_title} update failed {exception.Message}");
        }
    }

    private bool TryCreate()
    {
        var ui = Resources
            .FindObjectsOfTypeAll<PlayerUI>()
            .FirstOrDefault(candidate =>
                candidate != null
                && candidate.isActiveAndEnabled
                && candidate.gameObject.scene.IsValid()
                && candidate._mainPanel != null
                && candidate._dashDamageText?.font != null
            );
        if (ui == null || ui._dashDamageText.fontSharedMaterial == null)
            return false;

        Dispose();
        _ui = ui;
        var source = ui._dashDamageText;
        _root = new GameObject($"Archipelago {_title} Timer") { layer = source.gameObject.layer };
        var rect = _root.AddComponent<RectTransform>();
        rect.SetParent(ui._mainPanel.transform, false);
        rect.localPosition = new Vector3(0, TimedTrapHudLayout.Height(_row()), -0.05f);
        rect.localScale = new Vector3(0.0015625f, 0.0015625f, 0.0015625f);
        rect.sizeDelta = new Vector2(420, 64);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        _group = _root.AddComponent<CanvasGroup>();
        _group.interactable = false;
        _group.blocksRaycasts = false;

        var textObject = new GameObject("Label") { layer = _root.layer };
        var textRect = textObject.AddComponent<RectTransform>();
        textRect.SetParent(rect, false);
        _text = textObject.AddComponent<TextMeshProUGUI>();
        _text.font = source.font;
        _material = new Material(source.fontSharedMaterial) { name = $"Archipelago {_title} HUD Font" };
        if (_material.HasProperty("_FaceColor"))
            _material.SetColor("_FaceColor", Color.white);
        _text.fontSharedMaterial = _material;
        _text.fontStyle = source.fontStyle & ~(FontStyles.UpperCase | FontStyles.LowerCase | FontStyles.SmallCaps);
        _text.fontWeight = source.fontWeight;
        _text.fontSize = 24;
        _text.alignment = TextAlignmentOptions.Center;
        _text.enableAutoSizing = false;
        _text.enableWordWrapping = false;
        _text.richText = false;
        _text.raycastTarget = false;
        _text.maskable = false;
        _text.color = _color;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(0, 12);
        textRect.offsetMax = Vector2.zero;

        var track = new GameObject("Duration Track") { layer = _root.layer };
        var trackRect = track.AddComponent<RectTransform>();
        trackRect.SetParent(rect, false);
        trackRect.anchoredPosition = new Vector2(0, -20);
        trackRect.sizeDelta = new Vector2(400, 5);
        var background = track.AddComponent<Image>();
        background.color = ApColors.TimedTrapBackgroundColor;
        background.raycastTarget = false;
        var fill = new GameObject("Remaining") { layer = _root.layer };
        _fill = fill.AddComponent<RectTransform>();
        _fill.SetParent(trackRect, false);
        _fill.anchorMin = Vector2.zero;
        _fill.anchorMax = Vector2.one;
        _fill.offsetMin = Vector2.zero;
        _fill.offsetMax = Vector2.zero;
        var foreground = fill.AddComponent<Image>();
        foreground.color = _text.color;
        foreground.raycastTarget = false;

        _noBlockHudLog.Write(
            $"Created ui={ui.Pointer} parent={ui._mainPanel.name} scene={ui.gameObject.scene.name} font={source.font.name} position={rect.localPosition} layer={_root.layer}"
        );
        return true;
    }

    public void Hide()
    {
        if (_root != null)
            _root.SetActive(false);
    }

    public void Dispose()
    {
        if (_root != null)
            Object.Destroy(_root);
        if (_material != null)
            Object.Destroy(_material);
        _root = null;
        _ui = null;
        _text = null;
        _fill = null;
        _group = null;
        _material = null;
    }
}
