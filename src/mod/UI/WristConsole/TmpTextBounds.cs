using Il2CppTMPro;

namespace UntilYouFallArchipelago;

internal static class TmpTextBounds
{
    public static bool Exceeds(TextMeshProUGUI text) => CountOutside(text) > 0;

    public static int Clip(TextMeshProUGUI text)
    {
        var info = text.textInfo;
        var clipped = 0;
        for (var i = 0; i < info.characterCount; i++)
        {
            var character = info.characterInfo[i];
            if (!character.isVisible)
                continue;
            if (IsInside(text, character))
                continue;

            var colors = info.meshInfo[character.materialReferenceIndex].colors32;
            for (var vertex = character.vertexIndex; vertex < character.vertexIndex + 4; vertex++)
            {
                var color = colors[vertex];
                color.a = 0;
                colors[vertex] = color;
            }
            clipped++;
        }
        if (clipped > 0)
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);

        return clipped;
    }

    private static int CountOutside(TextMeshProUGUI text)
    {
        var count = 0;
        var info = text.textInfo;
        for (var i = 0; i < info.characterCount; i++)
            if (info.characterInfo[i].isVisible && !IsInside(text, info.characterInfo[i]))
                count++;

        return count;
    }

    private static bool IsInside(TextMeshProUGUI text, TMP_CharacterInfo character)
    {
        const float tolerance = 1f;
        var rect = text.rectTransform.rect;

        return character.bottomLeft.x >= rect.xMin - tolerance
            && character.topRight.x <= rect.xMax + tolerance
            && character.bottomLeft.y >= rect.yMin - tolerance
            && character.topRight.y <= rect.yMax + tolerance;
    }
}
