using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal sealed partial class WorldTextLabel : IDisposable
{
    private GameObject? _root;
    private TextMeshPro? _text;
    private Material? _material;
    private static TMP_FontAsset? _fallbackFont;

    private static TMP_FontAsset? FallbackFont() =>
        _fallbackFont != null
            ? _fallbackFont
            : _fallbackFont = Resources
                .FindObjectsOfTypeAll<TMP_Text>()
                .Select(candidate => candidate.font)
                .FirstOrDefault(candidate => candidate != null && candidate.material != null);

    public bool Create(
        Camera camera,
        string objectName,
        string value,
        Vector3 position,
        Quaternion rotation,
        TMP_FontAsset? preferredFont,
        Color color,
        bool overlay,
        Action<string> log,
        string logPrefix
    )
    {
        Dispose();
        var layer = WorldTextRules.FirstVisibleLayer(camera.cullingMask);
        if (layer < 0)
        {
            log.Write(logPrefix, $"Failed: camera {camera.name} renders no layers");
            return false;
        }

        var font = preferredFont ?? FallbackFont();
        if (font == null)
        {
            log.Write(logPrefix, "Failed: no loaded TMP font");
            return false;
        }

        _root = new GameObject(objectName) { layer = layer };
        var rect = _root.AddComponent<RectTransform>();
        rect.position = position;
        rect.rotation = rotation;
        rect.localScale = new Vector3(0.03f, 0.03f, 0.03f);
        rect.sizeDelta = new Vector2(40f, 5f);

        _text = _root.AddComponent<TextMeshPro>();
        _text.font = font;
        _material = new Material(font.material) { name = objectName + " Material" };
        if (_material.HasProperty("_FaceColor"))
            _material.SetColor("_FaceColor", color);
        TmpOverlayMaterial.Apply(_material, overlay ? CompareFunction.Always : CompareFunction.LessEqual);
        if (_material.HasProperty("_CullMode"))
            _material.SetInt("_CullMode", (int)CullMode.Off);
        _text.fontSharedMaterial = _material;
        _text.text = value;
        _text.color = color;
        _text.fontSize = 36f;
        _text.alignment = TextAlignmentOptions.Center;
        _text.enableWordWrapping = false;
        _text.enableCulling = false;
        _text.isOverlay = overlay;
        _text.ForceMeshUpdate();

        return true;
    }

    public void SetText(string value)
    {
        if (_text == null || _text.text == value)
            return;

        _text.text = value;
        _text.ForceMeshUpdate();
    }

    public void Dispose()
    {
        if (_root != null)
            Object.Destroy(_root);
        if (_material != null)
            Object.Destroy(_material);
        _root = null;
        _text = null;
        _material = null;
    }
}
