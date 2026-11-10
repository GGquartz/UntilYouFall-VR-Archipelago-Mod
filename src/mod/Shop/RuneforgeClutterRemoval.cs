using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSG.Claymore.Level;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal sealed class RuneforgeClutterRemoval : IDisposable
{
    private readonly LogChannel _log;
    private readonly List<(
        GameObject Visual,
        Mesh Mesh,
        MeshRenderer Original,
        bool WasEnabled
    )> _meshesWithoutPodium = [];
    private bool _done;
    private int _attempts;
    private long _retryAt;
    private long _readyAt;
    private bool _preparedBeforeFade;

    public RuneforgeClutterRemoval(Action<string> log) => _log = log.Channel("APRuneforgeClutter");

    public void BeforeFadeIn(LevelManager levelManager)
    {
        if (!levelManager.IsRuneforgeRoom())
            return;

        Reset();
        _preparedBeforeFade = true;
        _log.Write(
            $"Before fadein loading={levelManager.IsLoading} coreLoading={levelManager.IsCoreLoading} frame={Time.frameCount}"
        );
        Update(levelManager);
    }

    public void Update(LevelManager? levelManager)
    {
        if (!levelManager.IsRuneforgeRoom())
        {
            Reset();
            return;
        }
        if (!_preparedBeforeFade && levelManager.IsRoomLoading())
        {
            Reset();
            return;
        }
        if (_readyAt == 0)
            _readyAt = Environment.TickCount64;
        if (_done || Environment.TickCount64 < _retryAt)
            return;
        _retryAt = Environment.TickCount64 + 2000;
        try
        {
            var all = Resources
                .FindObjectsOfTypeAll<Transform>()
                .Where(transform => transform != null && transform.gameObject.scene.IsValid())
                .ToArray();
            var dummies = all.Where(transform =>
                    transform.gameObject.activeSelf && transform.name.Contains("TrainingDummy")
                )
                .ToArray();
            var charge = all.Where(transform =>
                    transform.gameObject.activeSelf && transform.name.Contains("SuperCharge")
                )
                .ToArray();
            charge = charge.Where(transform => Path(transform).StartsWith("Interactions/")).ToArray();

            foreach (var dummy in dummies)
            {
                dummy.gameObject.SetActive(false);
            }
            foreach (var crystal in charge)
            {
                crystal.gameObject.SetActive(false);
            }
            var podiums = all.Where(transform =>
                    transform.gameObject.activeSelf && Path(transform) == "Interactions/Super Charge Podium"
                )
                .ToArray();
            foreach (var podium in podiums)
            {
                podium.gameObject.SetActive(false);
            }
            var particles = Resources
                .FindObjectsOfTypeAll<ParticleSystem>()
                .Where(system =>
                    system != null
                    && system.gameObject.scene.IsValid()
                    && system.gameObject.activeInHierarchy
                    && Path(system.transform) == "DefaultObjs/FX_HubVFX/FX_TrainingEnergy"
                )
                .ToArray();
            foreach (var system in particles)
            {
                system.gameObject.SetActive(false);
            }
            if (_attempts == 0)
            {
                var top = VerifyPodiumMesh(all, "Atlas_23_sel_0", 466, 388, 75);
                var body = VerifyPodiumMesh(all, "Atlas_21_sel_0", 51070, 31076, 547);
                if (top.HasValue && body.HasValue)
                    SwapInMeshesWithoutPodium(top.Value, body.Value);
            }
            _attempts++;
            _done = _attempts >= 3;

            _log.Write(
                $"Pass {_attempts} dummies={dummies.Length} chargeObjects={charge.Length} podiums={podiums.Length} particles={particles.Length} done={_done}"
            );
        }
        catch (Exception exception)
        {
            _attempts++;
            _done = _attempts >= 5;
            _log.Write("Failed: " + exception);
        }
    }

    private (
        Mesh Mesh,
        MeshRenderer Renderer,
        Il2CppStructArray<int> Indices,
        int RetainedIndexCount
    )? VerifyPodiumMesh(
        Transform[] all,
        string meshName,
        int expectedVertices,
        int expectedTriangles,
        int expectedRemoved
    )
    {
        try
        {
            var matches = all.Where(transform => Path(transform) == "AtlasData/" + meshName).ToArray();
            if (matches.Length != 1)
            {
                _log.Write($"Pillar mesh match !=1 target={meshName} matches={matches.Length} readOnly=True");
                return null;
            }
            var target = matches[0];
            var mesh = target.GetComponent<MeshFilter>()?.sharedMesh;
            var renderer = target.GetComponent<MeshRenderer>();
            if (mesh == null || renderer == null)
            {
                _log.Write($"Pillar mesh skipped target={meshName} missingMeshOrRenderer=True readOnly=True");
                return null;
            }
            var identityFrame =
                target.position.sqrMagnitude < 0.00000001f
                && Quaternion.Angle(target.rotation, Quaternion.identity) < 0.001f
                && (target.lossyScale - Vector3.one).sqrMagnitude < 0.00000001f;
            if (!mesh.isReadable || !identityFrame || mesh.subMeshCount != 1 || mesh.GetBaseVertex(0) != 0)
            {
                _log.Write($"Pillar mesh skipped target={meshName} unsupportedRuntimeMesh=True readOnly=True");
                return null;
            }
            if (mesh.GetTopology(0) != MeshTopology.Triangles)
            {
                _log.Write($"Pillar mesh skipped target={meshName} nonTriangleTopology=True readOnly=True");
                return null;
            }
            if (mesh.vertexCount != expectedVertices || mesh.GetIndexCount(0) != expectedTriangles * 3)
            {
                _log.Write(
                    $"Pillar mesh skipped target={meshName} assetCountMismatch=True indexStart={mesh.GetIndexStart(0)} indices={mesh.GetIndexCount(0)} readOnly=True"
                );
                return null;
            }
            var vertices = mesh.vertices;
            var triangles = mesh.GetIndices(0, false);
            var inside = new bool[vertices.Length];
            var removedVertices = new bool[vertices.Length];
            var retainedVertices = new bool[vertices.Length];
            for (var vertex = 0; vertex < vertices.Length; vertex++)
            {
                var position = vertices[vertex];
                inside[vertex] =
                    position.x >= 4.85f
                    && position.x <= 5.49f
                    && position.y >= -0.05f
                    && position.y <= 1.30f
                    && position.z >= 16.25f
                    && position.z <= 16.86f;
            }
            var removedTriangles = 0;
            var crossingTriangles = 0;
            var tailOnly = true;
            var firstTailIndex = (expectedTriangles - expectedRemoved) * 3;
            for (var index = 0; index < triangles.Length; index += 3)
            {
                var first = triangles[index];
                var second = triangles[index + 1];
                var third = triangles[index + 2];
                var remove = inside[first] && inside[second] && inside[third];
                if (remove)
                    removedTriangles++;
                else if (inside[first] || inside[second] || inside[third])
                    crossingTriangles++;
                if (remove != (index >= firstTailIndex))
                    tailOnly = false;
                var used = remove ? removedVertices : retainedVertices;
                used[first] = used[second] = used[third] = true;
            }
            var sharedVertices = 0;
            for (var vertex = 0; vertex < vertices.Length; vertex++)
                if (removedVertices[vertex] && retainedVertices[vertex])
                    sharedVertices++;
            var matchesAsset =
                mesh.name == meshName
                && mesh.vertexCount == expectedVertices
                && triangles.Length == expectedTriangles * 3
                && removedTriangles == expectedRemoved
                && crossingTriangles == 0
                && sharedVertices == 0
                && tailOnly;
            if (!matchesAsset)
            {
                _log.Write(
                    $"Pillar mesh rejected target={meshName} "
                        + $"triangles={triangles.Length / 3} selected={removedTriangles} expectedSelected={expectedRemoved} "
                        + $"crossing={crossingTriangles} sharedVertices={sharedVertices} tailOnly={tailOnly} readOnly=True"
                );
                return null;
            }

            return (mesh, renderer, triangles, firstTailIndex);
        }
        catch (Exception exception)
        {
            _log.Write($"Pillar mesh failed target={meshName} readOnly=True error={exception}");
            return null;
        }
    }

    private void SwapInMeshesWithoutPodium(
        params (Mesh Mesh, MeshRenderer Renderer, Il2CppStructArray<int> Indices, int RetainedIndexCount)[] proofs
    )
    {
        try
        {
            foreach (var proof in proofs)
                _meshesWithoutPodium.Add(CreateMeshWithoutPodium(proof));
            foreach (var replacement in _meshesWithoutPodium)
                replacement.Original.enabled = false;
            foreach (var replacement in _meshesWithoutPodium)
                replacement.Visual.SetActive(true);
        }
        catch (Exception exception)
        {
            Dispose();
            _log.Write("Pillar originals restored error=" + exception);
        }
    }

    private (GameObject Visual, Mesh Mesh, MeshRenderer Original, bool WasEnabled) CreateMeshWithoutPodium(
        (Mesh Mesh, MeshRenderer Renderer, Il2CppStructArray<int> Indices, int RetainedIndexCount) proof
    )
    {
        GameObject? visual = null;
        Mesh? copy = null;
        try
        {
            copy = Object.Instantiate(proof.Mesh);
            copy.name = proof.Mesh.name + " AP Without Podium";
            var indices = new Il2CppStructArray<int>(proof.Indices.Take(proof.RetainedIndexCount).ToArray());
            copy.SetTriangles(indices, 0, false);
            if (copy.GetIndexCount(0) != proof.RetainedIndexCount)
                throw new InvalidOperationException("Podium replacement index count mismatch");
            visual = new GameObject("AP Atlas Without Podium");
            visual.SetActive(false);
            visual.transform.SetParent(proof.Renderer.transform, false);
            visual.layer = proof.Renderer.gameObject.layer;
            visual.AddComponent<MeshFilter>().sharedMesh = copy;
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = proof.Renderer.sharedMaterials;
            renderer.shadowCastingMode = proof.Renderer.shadowCastingMode;
            renderer.receiveShadows = proof.Renderer.receiveShadows;
            renderer.lightmapIndex = proof.Renderer.lightmapIndex;
            renderer.lightmapScaleOffset = proof.Renderer.lightmapScaleOffset;
            renderer.enabled = proof.Renderer.enabled;

            return (visual, copy, proof.Renderer, proof.Renderer.enabled);
        }
        catch
        {
            if (visual != null)
                Object.Destroy(visual);
            if (copy != null)
                Object.Destroy(copy);
            throw;
        }
    }

    public void Dispose()
    {
        foreach (var replacement in _meshesWithoutPodium)
        {
            if (replacement.Visual != null)
            {
                replacement.Visual.SetActive(false);
                Object.Destroy(replacement.Visual);
            }
            if (replacement.Original != null)
                replacement.Original.enabled = replacement.WasEnabled;
            if (replacement.Mesh != null)
                Object.Destroy(replacement.Mesh);
        }
        _meshesWithoutPodium.Clear();
    }

    private void Reset()
    {
        Dispose();
        _done = false;
        _attempts = 0;
        _retryAt = 0;
        _readyAt = 0;
        _preparedBeforeFade = false;
    }

    private static string Path(Transform transform)
    {
        var parts = new Stack<string>();
        for (var current = transform; current != null; current = current.parent)
            parts.Push(current.name);

        return string.Join("/", parts);
    }
}
