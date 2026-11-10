using Il2CppSG.Claymore.Armory;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal sealed class RuneforgeShopStand : IDisposable
{
    private readonly WeaponRackSlot _slot;
    private readonly MeshFilter _column;
    private readonly Canvas _canvas;
    private readonly TMP_Text _level;
    private readonly Image _background;
    private readonly MeshFilter[] _glow;
    private readonly List<Material> _glowMaterials = [];

    private RuneforgeShopStand(
        WeaponRackSlot slot,
        MeshFilter column,
        Canvas canvas,
        TMP_Text level,
        Image background,
        MeshFilter[] glow
    )
    {
        _slot = slot;
        _column = column;
        _canvas = canvas;
        _level = level;
        _background = background;
        _glow = glow;
    }

    public static RuneforgeShopStand? Find()
    {
        var slots = Resources
            .FindObjectsOfTypeAll<WeaponRackSlot>()
            .Where(slot => slot != null && slot.gameObject.scene.IsValid());
        foreach (var slot in slots)
        {
            var indicator = slot.levelIndicators?.FirstOrDefault(candidate => candidate != null);
            var level = indicator?.GetComponentInChildren<TextMeshProUGUI>(true);
            var canvas = level?.transform.parent?.GetComponent<Canvas>();
            var background = canvas
                ?.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(image => image.name == "Background");
            var column = slot.GetComponentsInChildren<MeshFilter>(true)
                .FirstOrDefault(filter => filter.sharedMesh != null && filter.sharedMesh.name == "WeaponStand");
            if (
                level == null
                || canvas == null
                || canvas.renderMode != RenderMode.WorldSpace
                || background == null
                || column == null
            )
                continue;

            var glow = column
                .transform.parent.GetComponentsInChildren<MeshFilter>(true)
                .Where(filter =>
                    filter.sharedMesh != null
                    && filter.GetComponent<MeshRenderer>() != null
                    && (filter.name == "WeaponStand_Display" || filter.name == "WeaponStand_Glow")
                )
                .ToArray();
            if (glow.Length != 2)
                continue;

            return new RuneforgeShopStand(slot, column, canvas, level, background, glow);
        }

        return null;
    }

    public Quaternion ItemRotation(Transform column) =>
        column.rotation * Quaternion.Inverse(_column.transform.rotation) * _slot.transform.rotation;

    public GameObject CreateLabel(Transform column, string name, string price, TMP_Text currency, Action<string> log)
    {
        var root = new GameObject(name);
        try
        {
            root.SetActive(false);
            root.layer = _canvas.gameObject.layer;
            var rect = root.AddComponent<RectTransform>();
            rect.sizeDelta = _canvas.GetComponent<RectTransform>().rect.size;
            SetRelativePose(rect, _canvas.transform, column);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _canvas.worldCamera;
            canvas.additionalShaderChannels = _canvas.additionalShaderChannels;
            canvas.sortingLayerID = _canvas.sortingLayerID;
            canvas.sortingOrder = _canvas.sortingOrder;

            var textRoot = new GameObject("Price") { layer = _level.gameObject.layer };
            var textRect = textRoot.AddComponent<RectTransform>();
            textRect.SetParent(rect, false);
            var sourceRect = _level.rectTransform;
            textRect.anchorMin = sourceRect.anchorMin;
            textRect.anchorMax = sourceRect.anchorMax;
            textRect.pivot = sourceRect.pivot;
            textRect.sizeDelta = sourceRect.sizeDelta;
            textRect.anchoredPosition3D = sourceRect.anchoredPosition3D;
            textRect.localRotation = sourceRect.localRotation;
            textRect.localScale = sourceRect.localScale;
            var text = textRoot.AddComponent<TextMeshProUGUI>();
            text.font = currency.font;
            text.fontSharedMaterial = currency.fontSharedMaterial;
            text.spriteAsset = currency.spriteAsset;
            text.color = currency.color;
            text.enableVertexGradient = currency.enableVertexGradient;
            text.colorGradient = currency.colorGradient;
            text.fontStyle = currency.fontStyle;
            text.fontSize = _level.fontSize * 0.66f;
            text.enableAutoSizing = true;
            text.fontSizeMax = _level.fontSize * 0.66f;
            text.fontSizeMin = 10;
            text.alignment = _level.alignment;
            text.margin = _level.margin;
            text.richText = true;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            text.text = price;
            root.SetActive(true);
            text.ForceMeshUpdate();
            log.Write(
                "APShop",
                $"Native plate; item={name}; source={_slot.name}/{_canvas.name}; "
                    + $"rect={rect.rect.size}; fontSize={text.fontSize}; background={_background.sprite?.name}; "
                    + $"scale={rect.lossyScale.x:R},{rect.lossyScale.y:R},{rect.lossyScale.z:R}; "
                    + $"text='{text.text}'; characters={text.textInfo.characterCount}"
            );

            return root;
        }
        catch
        {
            root.SetActive(false);
            Object.Destroy(root);
            throw;
        }
    }

    public void AddGlow(Transform column)
    {
        foreach (var source in _glow)
        {
            var root = new GameObject(source.name);
            SetRelativePose(root.transform, source.transform, column);
            RuneforgeRackCloner.CopyMesh(source, root);
            if (source.name != "WeaponStand_Glow")
                continue;
            var renderer = root.GetComponent<MeshRenderer>();
            var materials = renderer.sharedMaterials;
            for (var index = 0; index < materials.Length; index++)
            {
                var material = new Material(materials[index]);
                _glowMaterials.Add(material);
                material.SetColor("Color_6439DC96", ApColors.ShopStandGlowColor);
                material.SetColor("Color_E96E879E", ApColors.ShopStandTintColor);
                materials[index] = material;
            }
            renderer.sharedMaterials = materials;
        }
    }

    public void Dispose()
    {
        foreach (var material in _glowMaterials)
            if (material != null)
                Object.Destroy(material);
        _glowMaterials.Clear();
    }

    private void SetRelativePose(Transform target, Transform source, Transform column)
    {
        target.SetParent(column, false);
        target.localPosition = _column.transform.InverseTransformPoint(source.position);
        target.localRotation = Quaternion.Inverse(_column.transform.rotation) * source.rotation;
        var scale = source.lossyScale;
        var columnScale = _column.transform.lossyScale;
        target.localScale = new Vector3(scale.x / columnScale.x, scale.y / columnScale.y, scale.z / columnScale.z);
    }
}
