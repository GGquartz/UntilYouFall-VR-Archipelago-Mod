using Il2CppSG.Claymore.Level;
using Il2CppSG.Claymore.SaveSystem;
using Il2CppSG.Claymore.UI;
using Il2CppSG.GlobalEvents.Variables;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal sealed class AetherMultiplierHubLabel : IDisposable
{
    private const long BaseRetryMs = 2000;
    private const long MaxRetryMs = 8000;
    private readonly Func<int> _itemCount;
    private readonly LogChannel _log;
    private readonly WorldTextLabel _label = new();
    private AnchorCandidate? _source;
    private Camera? _camera;
    private long _retryAt;
    private bool _inRuneforge;
    private int _searchAttempt;
    private int _displayedCount = -1;

    public AetherMultiplierHubLabel(Func<int> itemCount, Action<string> log)
    {
        _itemCount = itemCount;
        _log = log.Channel("APMultiplierHUD");
    }

    public void Update(LevelManager? levelManager)
    {
        var inRuneforge = levelManager.InRuneforge();
        if (!inRuneforge)
        {
            if (_inRuneforge)
                _log.Write("Left runeforge, removing multiplier label");
            _inRuneforge = false;
            Dispose();

            return;
        }

        if (!_inRuneforge)
        {
            _inRuneforge = true;
            _retryAt = 0;
            _searchAttempt = 0;
            _log.Write("Entered runeforge, searching for the native aether display");
        }

        if (_source == null || !_source.IsAlive || _camera == null || !_camera.isActiveAndEnabled)
        {
            if (_source != null)
                _log.Write($"Anchor lost source={_source.Path} retrying");
            Dispose();
            if (Environment.TickCount64 >= _retryAt)
            {
                _retryAt = Environment.TickCount64 + Math.Min(BaseRetryMs << Math.Min(_searchAttempt, 2), MaxRetryMs);
                TryAttach(levelManager);
            }

            return;
        }

        UpdateText();
    }

    private void TryAttach(LevelManager? levelManager)
    {
        var camera = ActiveStereoCamera();
        if (camera == null)
        {
            _log.Write("No active stereo camera");
            return;
        }

        var live = levelManager?.armamentInventory;
        var inventories =
            live?.currency != null
                ? [live]
                : Resources
                    .FindObjectsOfTypeAll<ArmamentInventory>()
                    .Where(inventory => inventory != null && inventory.currency != null)
                    .ToArray();
        var currencies = inventories
            .Select(inventory => inventory.currency)
            .DistinctBy(currency => currency.Pointer)
            .ToArray();
        _searchAttempt++;

        _log.Write(
            $"Anchor search {_searchAttempt} camera={camera.name} inventories={inventories.Length} currencies={currencies.Length} values=[{string.Join(",", currencies.Select(currency => currency.Value))}]"
        );

        var candidates = FindCandidates(camera, currencies);

        var selected = candidates
            .OrderByDescending(candidate => candidate.Score)
            .FirstOrDefault(candidate => candidate.Score > 0);
        if (selected == null)
        {
            _log.Write($"Anchor search {_searchAttempt} found no aether anchor");
            return;
        }

        _source = selected;
        _camera = camera;
        var pose = SourcePose(selected, camera);
        var gold = ApColors.AnchoredLabelGoldColor;
        var count = AetherMultiplierRules.Clamp(_itemCount());
        var value = AetherMultiplierRules.FormatDisplay(count);
        if (
            !_label.Create(
                camera,
                "AP Aether Multiplier",
                value,
                pose.Position,
                selected.Rect.rotation,
                selected.Font,
                gold,
                overlay: false,
                _log.Sink,
                "APMultiplierHUD"
            )
        )
        {
            Dispose();
            return;
        }
        _displayedCount = count;

        _log.Write(
            $"Attached source={selected.Path} type={selected.Kind} score={selected.Score} sourceHeight={pose.Height:F4} offset={pose.Offset:F4} text='{value}'"
        );
    }

    private static List<AnchorCandidate> FindCandidates(Camera camera, IntVariable[] currencies)
    {
        var candidates = new List<AnchorCandidate>();
        var seen = new HashSet<IntPtr>();
        foreach (var host in Resources.FindObjectsOfTypeAll<IntVariableText>())
        {
            if (host == null)
                continue;
            var variableMatch =
                host._intVariable != null && currencies.Any(currency => currency.Pointer == host._intVariable.Pointer);
            if (host._textMeshPro != null)
                AddCandidate(
                    candidates,
                    seen,
                    host._textMeshPro,
                    "IntVariableText.TMP",
                    variableMatch,
                    camera,
                    currencies
                );
            if (host._text != null)
                AddCandidate(candidates, seen, host._text, "IntVariableText.Text", variableMatch, camera, currencies);
        }

        foreach (var view in Resources.FindObjectsOfTypeAll<CashOutView>())
            if (view != null && view.totalCurrencyDisplay != null)
                AddCandidate(
                    candidates,
                    seen,
                    view.totalCurrencyDisplay,
                    "CashOutView.totalCurrencyDisplay",
                    false,
                    camera,
                    currencies
                );

        foreach (var text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (text == null || seen.Contains(text.Pointer))
                continue;
            var path = HierarchyPath(text.transform);
            var numericMatch = currencies.Any(currency =>
                AetherHubAnchorRules.ContainsDisplayedValue(text.text, currency.Value)
            );
            if (!numericMatch && !HasNameHint(path))
                continue;
            AddCandidate(candidates, seen, text, "TMP fallback", false, camera, currencies);
        }

        return candidates;
    }

    private static void AddCandidate(
        List<AnchorCandidate> candidates,
        HashSet<IntPtr> seen,
        Component component,
        string kind,
        bool variableMatch,
        Camera camera,
        IntVariable[] currencies
    )
    {
        if (!seen.Add(component.Pointer))
            return;
        var rect = component.GetComponent<RectTransform>();
        if (rect == null)
            return;

        var tmp = component.TryCast<TMP_Text>();
        var legacy = component.TryCast<Text>();
        var text = tmp?.text ?? legacy?.text ?? "";
        var canvas = component.GetComponentInParent<Canvas>();
        var path = HierarchyPath(component.transform);
        if (!path.Contains("AetherCurrencyDisplay", StringComparison.OrdinalIgnoreCase))
            return;
        if (
            path.StartsWith("AP Aether Multiplier", StringComparison.Ordinal)
            || path.Contains("/AP Aether Multiplier/", StringComparison.Ordinal)
        )
            return;
        var active = component.gameObject.activeInHierarchy;
        var worldRenderable = canvas == null || canvas.renderMode == RenderMode.WorldSpace;
        var numericMatch = currencies.Any(currency =>
            AetherHubAnchorRules.ContainsDisplayedValue(text, currency.Value)
        );
        var nameHint = HasNameHint(path);
        var score = AetherHubAnchorRules.Score(variableMatch, active, worldRenderable, numericMatch, nameHint);

        candidates.Add(
            new AnchorCandidate(
                component,
                rect,
                kind,
                path,
                text,
                canvas,
                variableMatch,
                numericMatch,
                nameHint,
                score,
                camera.WorldToViewportPoint(rect.position)
            )
        );
    }

    private void UpdateText()
    {
        var count = AetherMultiplierRules.Clamp(_itemCount());
        if (count == _displayedCount)
            return;

        var value = AetherMultiplierRules.FormatDisplay(count);
        _label.SetText(value);
        _displayedCount = count;
        _log.Write($"Text updated itemCount={count} text='{value}'");
    }

    private static (Vector3 Position, float Height, float Offset) SourcePose(AnchorCandidate source, Camera camera)
    {
        var rect = source.Rect.rect;
        var bottomLeft = source.Rect.TransformPoint(new Vector3(rect.xMin, rect.yMin, 0));
        var bottomRight = source.Rect.TransformPoint(new Vector3(rect.xMax, rect.yMin, 0));
        var topLeft = source.Rect.TransformPoint(new Vector3(rect.xMin, rect.yMax, 0));
        var height = Vector3.Distance(bottomLeft, topLeft);
        var offset = AetherHubAnchorRules.OffsetBelow(height);
        var up = height > 0.0001f ? (topLeft - bottomLeft).normalized : camera.transform.up;

        return ((bottomLeft + bottomRight) * 0.5f - up * offset, height, offset);
    }

    private static Camera? ActiveStereoCamera()
    {
        var camera = SceneCache.StereoCamera();

        return camera != null && camera.isActiveAndEnabled ? camera : null;
    }

    private static bool HasNameHint(string value) =>
        value.Contains("aether", StringComparison.OrdinalIgnoreCase)
        || value.Contains("currency", StringComparison.OrdinalIgnoreCase)
        || value.Contains("forge", StringComparison.OrdinalIgnoreCase);

    private static string HierarchyPath(Transform transform)
    {
        var names = new List<string>();
        for (var current = transform; current != null && names.Count < 12; current = current.parent)
            names.Add(current.name);
        names.Reverse();

        return string.Join("/", names);
    }

    public void Dispose()
    {
        _label.Dispose();
        _source = null;
        _camera = null;
        _displayedCount = -1;
    }

    private sealed record AnchorCandidate(
        Component Component,
        RectTransform Rect,
        string Kind,
        string Path,
        string Text,
        Canvas? Canvas,
        bool VariableMatch,
        bool NumericMatch,
        bool NameHint,
        int Score,
        Vector3 Viewport
    )
    {
        public bool IsAlive => Component != null && Component.gameObject.activeInHierarchy;
        public TMP_FontAsset? Font => Component.TryCast<TMP_Text>()?.font;
    }
}
