using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSG.Claymore.Armaments;
using Il2CppSG.Claymore.Interaction;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal sealed class NativeModelCrushableVisual : IItemVisual
{
    private static readonly HashSet<string> Failures = [];

    private readonly CrushableVisuals _visuals;
    private readonly Il2CppReferenceArray<Renderer> _originalRenderers;
    private readonly bool[] _originalEnabled;
    private readonly List<GameObject> _roots;
    private readonly List<MeshRenderer> _renderers;
    private readonly List<Material> _ownedMaterials = [];
    private readonly List<Action> _undo = [];
    private readonly List<(Transform Target, Vector3 Position, Quaternion Rotation, Vector3 Scale)> _restore = [];

    private NativeModelCrushableVisual(
        CrushableVisuals visuals,
        Il2CppReferenceArray<Renderer> originals,
        bool[] originalEnabled,
        List<GameObject> roots,
        List<MeshRenderer> renderers
    )
    {
        _visuals = visuals;
        _originalRenderers = originals;
        _originalEnabled = originalEnabled;
        _roots = roots;
        _renderers = renderers;
    }

    public static bool HasFailed(string resource) => Failures.Contains(resource);

    public static NativeModelCrushableVisual? TryAttach(RewardInteractable reward, string resource, Action<string> log)
    {
        var visuals = reward.Visuals;
        var originals = visuals?.Renderers;
        if (visuals == null || originals == null || originals.Length == 0)
        {
            log.Write("ItemModel", $"Waiting resource={resource} visuals={(visuals == null ? "null" : "ok")}");
            return null;
        }
        var sourceIndex = -1;
        for (var index = 0; index < originals.Length; index++)
        {
            var renderer = originals[index];
            if (
                renderer != null
                && renderer.enabled
                && renderer.TryCast<MeshRenderer>() != null
                && renderer.sharedMaterial != null
            )
            {
                sourceIndex = index;
                break;
            }
        }
        if (sourceIndex < 0)
        {
            log.Write("ItemModel", $"Waiting resource={resource} no active source renderer");
            return null;
        }

        var roots = new List<GameObject>();
        var renderers = new List<MeshRenderer>();
        var ownedMaterials = new List<Material>();
        Transform? liveSource = null;
        CrushInteractable? liveInteractable = null;
        Matrix4x4? liveToShop = null;
        try
        {
            var donor =
                Resources.Load<GameObject>(resource) ?? throw new InvalidOperationException("Donor prefab not found");
            var reactors = donor.GetComponentsInChildren<CrushReactor>(true);
            var baseTransform = donor.GetComponentInChildren<CrushableVisuals>(true)?.transform ?? donor.transform;
            var toBase = baseTransform.worldToLocalMatrix;
            var rendererSummaries = new List<string>();
            foreach (var donorRenderer in donor.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = donorRenderer.GetComponent<MeshFilter>();
                var materials = donorRenderer.sharedMaterials;
                if (!donorRenderer.enabled || filter == null || filter.sharedMesh == null || materials.Length == 0)
                    continue;
                foreach (var reactor in reactors)
                    if (reactor.emissiveRenderer == donorRenderer)
                    {
                        for (var index = 0; index < materials.Length; index++)
                            if (reactor.targetIndex < 0 || reactor.targetIndex == index)
                            {
                                var material = new Material(materials[index]);
                                ownedMaterials.Add(material);
                                material.SetColor(reactor.emissionPropertyName, reactor.baselineGlow);
                                materials[index] = material;
                            }
                        log.Write(
                            "ItemModel",
                            $"Native baseline resource={resource} renderer={donorRenderer.name} property={reactor.emissionPropertyName} color={reactor.baselineGlow} index={reactor.targetIndex}"
                        );
                    }
                var relative = toBase * donorRenderer.transform.localToWorldMatrix;
                var forward = (Vector3)relative.GetColumn(2);
                var up = (Vector3)relative.GetColumn(1);
                var scale = new Vector3(((Vector3)relative.GetColumn(0)).magnitude, up.magnitude, forward.magnitude);
                var rotation = forward.sqrMagnitude > 0f ? Quaternion.LookRotation(forward, up) : Quaternion.identity;
                var position = (Vector3)relative.GetColumn(3);
                var live =
                    donorRenderer.name.Contains("Runestone")
                    || (Named(donor.transform, "GripRight") == null && !resource.Contains("SuperCharge"))
                        ? LiveSource(filter.sharedMesh, reward)
                        : null;
                var root = new GameObject("AP Item Model " + donorRenderer.name);
                root.transform.SetParent(visuals.transform, false);
                var upright = donorRenderer.name.Contains("Runestone");
                root.transform.localPosition = position;
                root.transform.localRotation = upright ? Quaternion.identity : rotation;
                root.transform.localScale = scale;
                if (live.HasValue)
                {
                    var liveMesh = live.Value.Root.worldToLocalMatrix * live.Value.Mesh.localToWorldMatrix;
                    var shopMesh = reward.transform.worldToLocalMatrix * root.transform.localToWorldMatrix;
                    liveSource = live.Value.Root;
                    liveInteractable = live.Value.Interactable;
                    liveToShop = shopMesh * liveMesh.inverse;
                }
                root.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                roots.Add(root);
                renderers.Add(renderer);
                rendererSummaries.Add(
                    $"{donorRenderer.name}:{filter.sharedMesh.name}:{materials.Length}:{materials[0].name}"
                );
            }
            if (renderers.Count == 0)
                throw new InvalidOperationException("Donor has no usable mesh renderer");

            var enabled = new bool[originals.Length];
            for (var index = 0; index < originals.Length; index++)
                enabled[index] = originals[index] != null && originals[index].enabled;
            originals[sourceIndex].enabled = false;
            var replacement = new Il2CppReferenceArray<Renderer>(originals.Length - 1 + renderers.Count);
            var write = 0;
            for (var index = 0; index < originals.Length; index++)
                if (index != sourceIndex)
                    replacement[write++] = originals[index];
            foreach (var renderer in renderers)
                replacement[write++] = renderer;
            visuals.renderers = replacement;
            log.Write("ItemModel", $"Attached resource={resource} renderers={string.Join(",", rendererSummaries)}");
            var result = new NativeModelCrushableVisual(visuals, originals, enabled, roots, renderers);
            result._ownedMaterials.AddRange(ownedMaterials);
            result.CopyGrip(
                reward.transform,
                liveSource ?? donor.transform,
                liveSource == null ? null : liveToShop,
                log
            );
            if (liveInteractable != null)
                result.CopyPoses(reward, liveInteractable, log);

            return result;
        }
        catch (Exception exception)
        {
            foreach (var root in roots)
                if (root != null)
                    Object.Destroy(root);
            foreach (var material in ownedMaterials)
                if (material != null)
                    Object.Destroy(material);
            Failures.Add(resource);

            log.Write("ItemModel", $"Fallback to AP logo resource={resource} failed: {exception}");
            return null;
        }
    }

    private static (CrushInteractable Interactable, Transform Root, Transform Mesh)? LiveSource(
        Mesh mesh,
        RewardInteractable self
    )
    {
        foreach (var candidate in Resources.FindObjectsOfTypeAll<CrushInteractable>())
        {
            if (
                candidate == null
                || !candidate.gameObject.scene.IsValid()
                || candidate.name.StartsWith("AP ")
                || candidate.Pointer == self.Pointer
            )
                continue;
            foreach (var filter in candidate.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh == mesh && !filter.name.StartsWith("AP Item Model"))
                    return (candidate, candidate.transform, filter.transform);
        }

        return null;
    }

    private void CopyPoses(CrushInteractable target, CrushInteractable source, Action<string> log)
    {
        var left = target.leftHandPose;
        var right = target.rightHandPose;
        var leftCrush = target.leftHandCrushingPose;
        var rightCrush = target.rightHandCrushingPose;
        _undo.Add(() =>
        {
            target.leftHandPose = left;
            target.rightHandPose = right;
            target.leftHandCrushingPose = leftCrush;
            target.rightHandCrushingPose = rightCrush;
        });
        target.leftHandPose = source.leftHandPose;
        target.rightHandPose = source.rightHandPose;
        target.leftHandCrushingPose = source.leftHandCrushingPose;
        target.rightHandCrushingPose = source.rightHandCrushingPose;
        log.Write(
            "ItemModel",
            $"Hand poses copied source={source.name} left={(source.leftHandPose == null ? "null" : source.leftHandPose.name)} right={(source.rightHandPose == null ? "null" : source.rightHandPose.name)}"
        );
    }

    private static Transform? Named(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == name);

    private void CopyGrip(Transform shop, Transform donor, Matrix4x4? liveToShop, Action<string> log)
    {
        foreach (var name in new[] { "GripRight", "GripLeft", "Collider" })
        {
            var from = Named(donor, name);
            if (from == null && liveToShop.HasValue)
            {
                var liveName = name switch
                {
                    "GripRight" => "Grip",
                    "Collider" => "Capsule",
                    _ => name,
                };
                from = Named(donor, liveName);
            }
            var to = Named(shop, name);
            log.Write("ItemModel", $"Grab geometry node={name} source={donor.name} found={from != null}/{to != null}");
            if (from == null || to == null)
                continue;
            if (name == "Collider")
            {
                var fromCapsule = from.GetComponent<CapsuleCollider>();
                var toCapsule = to.GetComponent<CapsuleCollider>();
                if (fromCapsule != null && toCapsule != null)
                {
                    toCapsule.center = fromCapsule.center;
                    toCapsule.radius = fromCapsule.radius;
                    toCapsule.height = fromCapsule.height;
                    toCapsule.direction = fromCapsule.direction;
                }
            }
            _restore.Add((to, to.localPosition, to.localRotation, to.localScale));
            if (liveToShop.HasValue)
            {
                var node = liveToShop.Value * (donor.worldToLocalMatrix * from.localToWorldMatrix);
                var nodeUp = (Vector3)node.GetColumn(1);
                var nodeForward = (Vector3)node.GetColumn(2);
                to.localPosition = node.GetColumn(3);
                to.localRotation = Quaternion.LookRotation(nodeForward, nodeUp);
                to.localScale = new Vector3(
                    ((Vector3)node.GetColumn(0)).magnitude,
                    nodeUp.magnitude,
                    nodeForward.magnitude
                );
                continue;
            }
            to.localPosition = from.localPosition;
            to.localRotation = from.localRotation;
            to.localScale = from.localScale;
        }
    }

    public void UseUnownedStyle(ArmamentVisualPreview style)
    {
        foreach (var renderer in _renderers)
        {
            var old = renderer.sharedMaterials;
            var replacement = new Il2CppReferenceArray<Material>(old.Length);
            for (var index = 0; index < old.Length; index++)
            {
                var material = new Material(old[index]) { shader = style.unownedShader };
                foreach (var color in style.unownedColors)
                    material.SetColor(color.propertyName, color.color);
                replacement[index] = material;
                _ownedMaterials.Add(material);
            }
            renderer.sharedMaterials = replacement;
        }
    }

    public void Dispose()
    {
        foreach (var undo in _undo)
            undo();
        foreach (var saved in _restore)
            if (saved.Target != null)
            {
                saved.Target.localPosition = saved.Position;
                saved.Target.localRotation = saved.Rotation;
                saved.Target.localScale = saved.Scale;
            }
        if (_visuals != null)
        {
            _visuals.renderers = _originalRenderers;
            for (var index = 0; index < _originalRenderers.Length; index++)
                if (_originalRenderers[index] != null)
                    _originalRenderers[index].enabled = _originalEnabled[index];
        }
        foreach (var root in _roots)
            if (root != null)
            {
                root.SetActive(false);
                Object.Destroy(root);
            }
        foreach (var material in _ownedMaterials)
            if (material != null)
                Object.Destroy(material);
    }
}
