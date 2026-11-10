using Il2CppSG.Claymore.Armory;
using Il2CppSG.Claymore.Level;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal sealed class RuneforgeRackCloner
{
    private readonly LogChannel _log;

    public RuneforgeRackCloner(Action<string> log) => _log = log.Channel("APRackDiag");

    private List<Transform> FindRackRoots()
    {
        var roots = new List<Transform>();
        foreach (var slot in Resources.FindObjectsOfTypeAll<WeaponRackSlot>())
        {
            if (slot == null || !slot.gameObject.scene.IsValid())
                continue;
            var node = slot.transform;
            while (node.parent != null && node.GetComponentsInChildren<WeaponRackSlot>(true).Length < 4)
                node = node.parent;
            if (!roots.Contains(node))
                roots.Add(node);
        }

        return roots;
    }

    public GameObject? CreateSection(LevelManager? levelManager, bool ignoreLoading = false)
    {
        if (!levelManager.InRuneforge(ignoreLoading))
        {
            _log.Write("Runeforge not loaded");
            return null;
        }
        var roots = FindRackRoots();
        var camera = Camera.main;
        if (roots.Count == 0 || camera == null)
        {
            _log.Write($"Clone failed roots={roots.Count} camera={camera != null}");
            return null;
        }
        GameObject? clone = null;
        try
        {
            var source = roots.OrderBy(root => root.name).First();
            clone = new GameObject("AP Shop Rack");
            clone.SetActive(false);
            var forward = camera.transform.forward;
            forward.y = 0;
            var position = camera.transform.position + forward.normalized * 4f;
            position.y = source.position.y;
            clone.transform.SetPositionAndRotation(position, source.rotation);
            clone.transform.localScale = source.lossyScale;
            var transforms = new Dictionary<IntPtr, Transform> { [source.Pointer] = clone.transform };
            var copied = 0;
            foreach (var sourceFilter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = sourceFilter.sharedMesh;
                if (mesh == null || (mesh.name != "WeaponStand" && mesh.name != "HUB_Weapon_Rack_Backboard"))
                    continue;
                var sourceRenderer = sourceFilter.GetComponent<MeshRenderer>();
                if (sourceRenderer == null || mesh.vertexCount == 0)
                    continue;
                var target = CopyTransform(sourceFilter.transform, transforms).gameObject;
                CopyMesh(sourceFilter, target);
                copied++;
            }
            if (copied == 0)
            {
                _log.Write("No structural meshes");
                Object.Destroy(clone);
                return null;
            }
            copied += AddFrontColumns(clone.transform, camera);
            clone.SetActive(true);
            var renderers = clone.GetComponentsInChildren<MeshRenderer>(true);

            _log.Write(
                $"Visual probe placed source={source.name} pos={position} "
                    + $"meshes={copied} stillBatched={renderers.Count(renderer => renderer.isPartOfStaticBatch)} "
                    + "colliders=0 gameplayComponents=0"
            );
            return clone;
        }
        catch (Exception exception)
        {
            _log.Write($"Visual probe failed: {exception}");
            if (clone != null)
                Object.Destroy(clone);

            return null;
        }
    }

    internal static void CopyMesh(MeshFilter sourceFilter, GameObject target)
    {
        var mesh = sourceFilter.sharedMesh;
        var sourceRenderer = sourceFilter.GetComponent<MeshRenderer>();
        target.layer = sourceFilter.gameObject.layer;
        var filter = target.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = target.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = sourceRenderer.sharedMaterials;
        renderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
        renderer.receiveShadows = sourceRenderer.receiveShadows;
    }

    private int AddFrontColumns(Transform root, Camera camera)
    {
        var columns = root.GetComponentsInChildren<MeshFilter>(true)
            .Where(filter => filter.sharedMesh != null && filter.sharedMesh.name == "WeaponStand")
            .OrderBy(filter => root.InverseTransformPoint(filter.transform.position).x)
            .ToArray();
        if (columns.Length != 4)
            throw new InvalidOperationException($"Only {columns.Length} columns, should be 4");

        var frontSign = root.InverseTransformPoint(camera.transform.position).z < 0 ? -1f : 1f;
        var frontZ =
            columns.Max(filter => frontSign * root.InverseTransformPoint(filter.transform.position).z) * frontSign
            + frontSign * 0.75f;
        foreach (var sourceFilter in columns.Skip(1).Take(2))
        {
            var target = new GameObject("AP Front Column").transform;
            target.SetParent(sourceFilter.transform.parent, false);
            target.localRotation = sourceFilter.transform.localRotation;
            target.localScale = sourceFilter.transform.localScale;
            var localPosition = root.InverseTransformPoint(sourceFilter.transform.position);
            localPosition.z = frontZ;
            target.position = root.TransformPoint(localPosition);
            var bottom = new Vector3(0, sourceFilter.sharedMesh.bounds.min.y, 0);
            var basePosition = target.TransformPoint(bottom);
            var scale = target.localScale;
            target.localScale = new Vector3(scale.x, scale.y * 0.8f, scale.z);
            target.position += basePosition - target.TransformPoint(bottom) + Vector3.up * 0.1f;
            CopyMesh(sourceFilter, target.gameObject);
        }

        return 2;
    }

    private static Transform CopyTransform(Transform source, Dictionary<IntPtr, Transform> transforms)
    {
        if (transforms.TryGetValue(source.Pointer, out var existing))
            return existing;
        var parent = CopyTransform(source.parent, transforms);
        var copy = new GameObject(source.name).transform;
        copy.SetParent(parent, false);
        copy.localPosition = source.localPosition;
        copy.localRotation = source.localRotation;
        copy.localScale = source.localScale;
        transforms.Add(source.Pointer, copy);

        return copy;
    }
}
