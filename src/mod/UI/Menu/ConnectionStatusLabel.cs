using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UntilYouFallArchipelago;

internal abstract class ConnectionStatusLabel
{
    private static readonly Color TextColor = ApColors.ModAccentColor.WithAlpha(0.9f);

    private readonly Func<bool> _isConnected;
    private readonly Action<string> _log;
    private readonly string _category;
    private readonly string _objectName;
    private GameObject? _root;
    private TextMeshProUGUI? _text;
    private bool? _displayedConnection;

    protected ConnectionStatusLabel(Func<bool> isConnected, Action<string> log, string category, string objectName)
    {
        _isConnected = isConnected;
        _log = log;
        _category = category;
        _objectName = objectName;
    }

    public void Update()
    {
        if (_text != null)
            RefreshText();
    }

    protected void Log(string message) => _log($"[{_category}] {message}");

    protected void ShowLabel(Func<TextMeshProUGUI?> create)
    {
        try
        {
            if (_root == null || _text == null)
                _text = create();
            if (_text == null)
                return;

            RefreshText();
            _root!.SetActive(true);
        }
        catch (Exception ex)
        {
            Log($"Failed to show mod status: {ex.GetType().Name}: {ex.Message}");
        }
    }

    protected TextMeshProUGUI CreateText(
        Transform parent,
        TextMeshProUGUI source,
        float fontSize,
        out RectTransform rect
    )
    {
        _root = new GameObject(_objectName) { layer = parent.gameObject.layer };
        rect = _root.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        _root.AddComponent<LayoutElement>().ignoreLayout = true;

        var text = _root.AddComponent<TextMeshProUGUI>();
        text.font = source.font;
        text.fontSharedMaterial = source.fontSharedMaterial;
        text.color = TextColor;
        text.alignment = TextAlignmentOptions.Center;
        text.richText = true;
        text.enableAutoSizing = false;
        text.fontSize = fontSize;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        text.text = ModMetadata.ConnectionStatusText(_isConnected());
        _displayedConnection = null;

        return text;
    }

    protected void LogCreated(TextMeshProUGUI source, Transform parent, RectTransform rect, TextMeshProUGUI text) =>
        Log(
            $"Mod status created source={source.name} parent={parent.name} position={rect.anchoredPosition} size={rect.sizeDelta} fontSize={text.fontSize}"
        );

    private void RefreshText()
    {
        var connected = _isConnected();
        if (_text == null || _displayedConnection == connected)
            return;

        _text.text = ModMetadata.ConnectionStatusText(connected);
        _displayedConnection = connected;

        Log($"Mod status updated connected={connected}");
    }
}
