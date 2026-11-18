using UnityEngine;
using UnityEngine.Rendering;

namespace UntilYouFallArchipelago;

internal static class TmpOverlayMaterial
{
    public static Material Copy(Material source, string name, Color faceColor)
    {
        var material = new Material(source) { name = name };
        if (material.HasProperty("_FaceColor"))
            material.SetColor("_FaceColor", faceColor);

        return material;
    }

    public static void Apply(Material material, CompareFunction depthTest = CompareFunction.Always)
    {
        if (material.HasProperty("_ZTestMode"))
            material.SetInt("_ZTestMode", (int)depthTest);
        if (material.HasProperty("_StencilComp"))
            material.SetInt("_StencilComp", (int)CompareFunction.Always);
        if (material.HasProperty("_Stencil"))
            material.SetInt("_Stencil", 0);
        if (material.HasProperty("_ColorMask"))
            material.SetInt("_ColorMask", (int)ColorWriteMask.All);
    }
}
