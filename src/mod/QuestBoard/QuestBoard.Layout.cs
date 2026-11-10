using System.Text.Json;
using Il2CppSG.Claymore.Interaction;
using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.UI;
using Il2CppSG.XR;
using Il2CppTMPro;
using Il2CppValve.VR;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal sealed partial class QuestBoard : IDisposable
{
    private static Image? _arrowTemplate;
    private static GameObject? _nativePanel;

    private void AddArrow(RectTransform root, float x, int step)
    {
        if (_arrowTemplate == null)
        {
            var source = Resources
                .FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(go =>
                    go != null
                    && go.name == "ArrowPanel"
                    && go.scene.IsValid()
                    && go.GetComponent<RectTransform>() != null
                );
            _arrowTemplate = source?.GetComponentsInChildren<Image>(true).FirstOrDefault(found => found.sprite != null);
        }
        var template = _arrowTemplate;
        if (template == null)
        {
            _log.Write("Native ArrowPanel sprite not found");
            return;
        }

        var center = new Vector2(x, PageTop - 330f);
        var group = NewRect("ArrowGroup" + step, root, Vector2.zero, new Vector2(InnerW, InnerH));
        var card = new Card
        {
            Index = step < 0 ? PreviousPageLogIndex : NextPageLogIndex,
            IsNavigation = true,
            Section = step,
            Owner = group.gameObject,
            FillWidth = 120f,
            Button = Box("Arrow" + step, group, center, new Vector2(120f, 200f), Color.clear),
        };
        card.Face = card.Button.GetComponent<Image>();
        card.Fill = Fill(
            "ArrowFill" + step,
            group,
            center + new Vector2(-60f, 0f),
            new Vector2(0f, 200f),
            ApColors.QuestBoardAccentColor.WithAlpha(0.55f)
        );
        var arrow = Box("AP Arrow" + step, group, center, new Vector2(220f, 220f), Color.white);
        var image = arrow.GetComponent<Image>();
        image.sprite = template.sprite;
        image.material = template.material;
        image.color = template.color;
        image.preserveAspect = true;
        arrow.localRotation = Quaternion.Euler(0f, 0f, step < 0 ? 180f : 0f);
        card.Fill.gameObject.SetActive(false);
        card.OnActivate = () => ShowPage(_page + step);

        _cards.Add(card);
    }

    private void ShowPage(int page)
    {
        _page = Math.Clamp(page, 0, _pages.Count - 1);
        for (var i = 0; i < _pages.Count; i++)
            _pages[i].SetActive(i == _page);
        foreach (var card in _cards)
            if (card.IsNavigation)
                card.Owner.SetActive(card.Section < 0 ? _page > 0 : _page < _pages.Count - 1);
        _log.Write($"Page {_page + 1}/{_pages.Count}");
    }

    private Transform? BuildBackground(RectTransform root)
    {
        if (_nativePanel == null)
            _nativePanel = Resources
                .FindObjectsOfTypeAll<GameObject>()
                .FirstOrDefault(go =>
                    go != null
                    && go.name == "Panel"
                    && go.scene.IsValid()
                    && go.transform.Find("Tutorial_Background") != null
                );
        var panel = _nativePanel;
        if (panel == null)
        {
            _log.Write("Native panel not found");
            Box(
                "FallbackBack",
                root,
                new Vector2(0f, BoundsOffsetY),
                new Vector2(BoundsW, BoundsH),
                ApColors.QuestBoardFallbackBackColor
            );
            return null;
        }

        var clone = Object.Instantiate(panel, root);
        clone.name = "AP Native Panel Background";
        clone.SetActive(true);
        foreach (var script in clone.GetComponentsInChildren<MonoBehaviour>(true))
            if ((script.GetIl2CppType().Namespace ?? "").StartsWith("SG", StringComparison.Ordinal))
                script.enabled = false;
        foreach (var animator in clone.GetComponentsInChildren<Animator>(true))
            animator.enabled = false;
        var background = clone.transform.Find("Tutorial_Background");
        for (var i = 0; i < clone.transform.childCount; i++)
        {
            var child = clone.transform.GetChild(i);
            if (child != background)
                child.gameObject.SetActive(false);
        }
        var rect = clone.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(900f, 600f);
        rect.localPosition = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;

        return background;
    }

    private void FitBackground(Transform? background, Vector3 center, float worldScale)
    {
        if (background == null)
            return;

        var filter = background.GetComponentInChildren<MeshFilter>(true);
        var mesh = filter != null ? filter.sharedMesh : null;
        if (filter == null || mesh == null)
        {
            _log.Write("Background has no mesh");
            return;
        }

        var target = filter.transform;
        var parent = target.parent != null ? target.parent.lossyScale : Vector3.one;
        var size = mesh.bounds.size;
        var local = target.localScale;
        local.x = BoundsW * worldScale / (size.x * parent.x);
        local.y = PanelHeightFix * BoundsH * worldScale / (size.y * parent.y);
        target.localScale = local;
        var worldCenter = target.TransformPoint(mesh.bounds.center);
        target.position += new Vector3(
            center.x - worldCenter.x,
            center.y + BoundsOffsetY * worldScale - worldCenter.y,
            0f
        );

        _log.Write(
            $"Background mesh={mesh.name} meshBounds={size} localScale={target.localScale} heightFix={PanelHeightFix}"
        );
    }

    private RectTransform Box(string name, RectTransform parent, Vector2 center, Vector2 size, Color color)
    {
        var rect = NewRect(name, parent, center, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return rect;
    }

    private RectTransform Fill(string name, RectTransform parent, Vector2 leftCenter, Vector2 size, Color color)
    {
        var rect = Box(name, parent, leftCenter, size, color);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = leftCenter;

        return rect;
    }

    private RectTransform Label(
        string name,
        RectTransform parent,
        Vector2 center,
        Vector2 size,
        string value,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment
    )
    {
        var rect = NewRect(name, parent, center, size);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = _font;
        text.fontSharedMaterial = _material;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        text.isOverlay = false;
        rect.sizeDelta = size;
        rect.anchoredPosition = center;

        return rect;
    }

    private static RectTransform NewRect(string name, RectTransform parent, Vector2 center, Vector2 size)
    {
        var rect = new GameObject(name).AddComponent<RectTransform>();

        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = center;
        rect.localPosition = new Vector3(rect.localPosition.x, rect.localPosition.y, -15f);

        return rect;
    }

    private static void SetLayerAll(GameObject root, int layer)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;
    }
}
