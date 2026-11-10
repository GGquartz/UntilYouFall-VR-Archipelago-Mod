using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSG.Claymore.Armaments;
using Il2CppSG.Claymore.Interaction;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal sealed class ApLogoCrushableVisual : IItemVisual
{
    private readonly CrushableVisuals _visuals;
    private readonly Il2CppReferenceArray<Renderer> _originalRenderers;
    private readonly bool[] _originalEnabled;
    private readonly GameObject _root;
    private readonly Mesh _mesh;
    private readonly Material[] _materials;
    private readonly Texture2D[] _albedoTextures;

    private ApLogoCrushableVisual(
        CrushableVisuals visuals,
        Il2CppReferenceArray<Renderer> originalRenderers,
        bool[] originalEnabled,
        GameObject root,
        Mesh mesh,
        Material[] materials,
        Texture2D[] albedoTextures
    )
    {
        _visuals = visuals;
        _originalRenderers = originalRenderers;
        _originalEnabled = originalEnabled;
        _root = root;
        _mesh = mesh;
        _materials = materials;
        _albedoTextures = albedoTextures;
    }

    public void UseUnownedStyle(ArmamentVisualPreview style)
    {
        var renderer = _root.GetComponent<MeshRenderer>();
        var replacement = new Il2CppReferenceArray<Material>(_materials.Length);
        for (var index = 0; index < _materials.Length; index++)
        {
            var old = _materials[index];
            var material = new Material(old) { shader = style.unownedShader };
            foreach (var color in style.unownedColors)
                material.SetColor(color.propertyName, color.color);
            replacement[index] = material;
            _materials[index] = material;
            Object.Destroy(old);
        }
        renderer.sharedMaterials = replacement;
        foreach (var original in _originalRenderers)
            if (original != null)
                original.enabled = false;
    }

    public static ApLogoCrushableVisual? TryAttach(RewardInteractable reward, Action<string> log)
    {
        var visuals = reward.Visuals;
        var originalRenderers = visuals?.Renderers;
        if (visuals == null)
        {
            log.Write("APLogoVisual", "Waiting reward.Visuals=null");
            return null;
        }
        if (originalRenderers == null || originalRenderers.Length == 0)
        {
            log.Write("APLogoVisual", $"Waiting renderers={(originalRenderers == null ? "null" : "0")}");
            return null;
        }

        Renderer? sourceRenderer = null;
        var sourceRendererIndex = -1;
        for (var index = 0; index < originalRenderers.Length; index++)
        {
            var renderer = originalRenderers[index];
            if (
                renderer != null
                && renderer.enabled
                && renderer.TryCast<MeshRenderer>() != null
                && renderer.sharedMaterial != null
            )
            {
                sourceRenderer = renderer;
                sourceRendererIndex = index;
                break;
            }
        }
        if (sourceRenderer == null)
        {
            log.Write("APLogoVisual", "Fallback - no active mesh renderer with a material");
            return null;
        }

        GameObject? root = null;
        Mesh? mesh = null;
        Material[] materials = Array.Empty<Material>();
        Texture2D[] albedoTextures = Array.Empty<Texture2D>();
        try
        {
            using var resource =
                typeof(ApLogoCrushableVisual).Assembly.GetManifestResourceStream(ApLogoMeshData.ResourceName)
                ?? throw new InvalidOperationException($"Embedded resource {ApLogoMeshData.ResourceName} is missing");
            var data = ApLogoMeshData.Read(resource, ApLogoMeshData.ResourceName);
            mesh = CreateMesh(data);
            mesh.name = "Archipelago Logo Mesh";

            root = new GameObject("Archipelago Logo Visual");
            root.transform.SetParent(visuals.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = new Vector3(ApLogoVisualRules.LocalScaleX, 1f, 1f);
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var logoRenderer = root.AddComponent<MeshRenderer>();

            var sourceAlbedo = sourceRenderer.sharedMaterial.HasProperty(ApLogoVisualRules.AlbedoTextureProperty)
                ? sourceRenderer.sharedMaterial.GetTexture(ApLogoVisualRules.AlbedoTextureProperty)
                : null;
            materials = CreateMaterials(
                sourceRenderer.sharedMaterial,
                data.Colors,
                out albedoTextures,
                out var colorProperties
            );
            var materialArray = new Il2CppReferenceArray<Material>(materials.Length);
            for (var index = 0; index < materials.Length; index++)
                materialArray[index] = materials[index];
            logoRenderer.sharedMaterials = materialArray;

            var enabled = new bool[originalRenderers.Length];
            var rendererNames = new string[originalRenderers.Length];
            for (var index = 0; index < originalRenderers.Length; index++)
            {
                var renderer = originalRenderers[index];
                enabled[index] = renderer != null && renderer.enabled;
                rendererNames[index] = renderer?.name ?? "<null>";
                if (!ApLogoVisualRules.KeepOriginalRenderer(index, sourceRendererIndex) && renderer != null)
                    renderer.enabled = false;
            }

            var replacement = new Il2CppReferenceArray<Renderer>(originalRenderers.Length);
            for (var index = 0; index < originalRenderers.Length; index++)
                replacement[index] = ApLogoVisualRules.KeepOriginalRenderer(index, sourceRendererIndex)
                    ? originalRenderers[index]
                    : logoRenderer;
            visuals.renderers = replacement;

            var triangleCount = data.SubmeshTriangles.Sum(indices => indices.Length / 3);

            log.Write(
                "APLogoVisual",
                $"Attached vertices={data.VertexCount} submeshes={data.SubmeshTriangles.Length} "
                    + $"triangles={triangleCount} shader='{sourceRenderer.sharedMaterial.shader?.name}' "
                    + $"properties={string.Join(",", colorProperties)} sourceAlbedo='{sourceAlbedo?.name}' "
                    + $"neutralAlbedo={sourceAlbedo != null} originalRenderers={string.Join(",", rendererNames)} "
                    + $"bounds={mesh.bounds.size} localPosition={root.transform.localPosition} "
                    + $"localRotation={root.transform.localRotation.eulerAngles} localScale={root.transform.localScale}"
            );

            return new ApLogoCrushableVisual(
                visuals,
                originalRenderers,
                enabled,
                root,
                mesh,
                materials,
                albedoTextures
            );
        }
        catch (Exception exception)
        {
            if (root != null)
                Object.Destroy(root);
            foreach (var material in materials)
                if (material != null)
                    Object.Destroy(material);
            foreach (var texture in albedoTextures)
                if (texture != null)
                    Object.Destroy(texture);
            if (mesh != null)
                Object.Destroy(mesh);

            log.Write("APLogoVisual", $"Fallback - attach failed: {exception}");
            return null;
        }
    }

    private static Mesh CreateMesh(ApLogoMeshData data)
    {
        var vertices = new Il2CppStructArray<Vector3>(data.VertexCount);
        var normals = new Il2CppStructArray<Vector3>(data.VertexCount);
        var uvs = new Il2CppStructArray<Vector2>(data.VertexCount);
        for (var vertex = 0; vertex < data.VertexCount; vertex++)
        {
            vertices[vertex] = new Vector3(
                data.Positions[vertex * 3],
                data.Positions[vertex * 3 + 1],
                data.Positions[vertex * 3 + 2]
            );
            normals[vertex] = new Vector3(
                data.Normals[vertex * 3],
                data.Normals[vertex * 3 + 1],
                data.Normals[vertex * 3 + 2]
            );
            uvs[vertex] = new Vector2(data.Uvs[vertex * 2], data.Uvs[vertex * 2 + 1]) * 1f;
        }

        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = data.SubmeshTriangles.Length;
        for (var submesh = 0; submesh < data.SubmeshTriangles.Length; submesh++)
        {
            var source = data.SubmeshTriangles[submesh];
            var triangles = new Il2CppStructArray<int>(source.Length);
            for (var index = 0; index < source.Length; index++)
                triangles[index] = source[index];
            mesh.SetTriangles(triangles, submesh, false);
        }
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        var smoothNormals = mesh.normals;
        var tangents = new Il2CppStructArray<Vector4>(data.VertexCount);
        for (var vertex = 0; vertex < data.VertexCount; vertex++)
        {
            var axis = Mathf.Abs(smoothNormals[vertex].y) > 0.9f ? Vector3.right : Vector3.up;
            var tangent = Vector3.Cross(smoothNormals[vertex], axis).normalized;
            tangents[vertex] = new Vector4(tangent.x, tangent.y, tangent.z, 1f);
        }
        mesh.SetTangents(tangents);
        mesh.UploadMeshData(true);

        return mesh;
    }

    private static Material[] CreateMaterials(
        Material source,
        ApLogoColor[] colors,
        out Texture2D[] albedoTextures,
        out string[] usedColorProperties
    )
    {
        var used = new HashSet<string>();
        var materials = new Material[colors.Length];
        albedoTextures = new Texture2D[colors.Length];
        for (var index = 0; index < colors.Length; index++)
        {
            var color = ApLogoVisualRules.DisplayColor(colors[index]);
            var material = new Material(source) { name = $"Archipelago Logo Material {index + 1}" };
            foreach (var property in ApLogoVisualRules.ColorProperties)
            {
                if (!material.HasProperty(property))
                    continue;
                material.SetColor(property, new Color(color.R, color.G, color.B, color.A));
                used.Add(property);
            }
            foreach (var property in ApLogoVisualRules.EmissionProperties)
            {
                if (!material.HasProperty(property))
                    continue;
                var emission = ApLogoVisualRules.Emission(color);
                material.SetColor(property, new Color(emission.R, emission.G, emission.B, emission.A));
                material.EnableKeyword("_EMISSION");
                used.Add(property);
            }
            if (material.HasProperty(ApLogoVisualRules.AlbedoTextureProperty))
            {
                var albedo = new Texture2D(1, 1) { name = $"Archipelago Logo Albedo {index + 1}" };
                albedo.SetPixel(0, 0, new Color(color.R, color.G, color.B, color.A));
                albedo.Apply(false, true);
                albedoTextures[index] = albedo;
                material.SetTexture(ApLogoVisualRules.AlbedoTextureProperty, albedo);
            }
            if (material.HasProperty(ApLogoVisualRules.AlbedoContributionProperty))
                material.SetFloat(ApLogoVisualRules.AlbedoContributionProperty, ApLogoVisualRules.AlbedoContribution);
            if (material.HasProperty(ApLogoVisualRules.PulseFloorProperty))
                material.SetFloat(ApLogoVisualRules.PulseFloorProperty, ApLogoVisualRules.PulseFloor);
            materials[index] = material;
        }
        usedColorProperties = used.OrderBy(value => value).ToArray();

        return materials;
    }

    public void Dispose() => Dispose(true);

    private void Dispose(bool restoreOriginals)
    {
        if (_visuals != null)
        {
            _visuals.renderers = _originalRenderers;
            for (var index = 0; index < _originalRenderers.Length; index++)
                if (_originalRenderers[index] != null)
                    _originalRenderers[index].enabled = restoreOriginals && _originalEnabled[index];
        }
        if (_root != null)
            _root.SetActive(false);
        if (_root != null)
            Object.Destroy(_root);
        foreach (var material in _materials)
            if (material != null)
                Object.Destroy(material);
        foreach (var texture in _albedoTextures)
            if (texture != null)
                Object.Destroy(texture);
        if (_mesh != null)
            Object.Destroy(_mesh);
    }
}
