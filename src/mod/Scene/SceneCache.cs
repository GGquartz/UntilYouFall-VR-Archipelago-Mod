using Il2CppSG.Claymore.Armaments;
using Il2CppSG.Claymore.Armaments.Traits;
using Il2CppSG.Claymore.Interaction.Rewards;
using Il2CppSG.Claymore.UI;
using Il2CppTMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UntilYouFallArchipelago;

internal static class SceneCache
{
    private const string MenuFontName = "Lovato Bold SDF";

    private static TMP_FontAsset? _menuFont;
    private static ArmamentVisualPreview? _ghostStyle;
    private static readonly Dictionary<string, AudioClip> Clips = [];
    private static readonly Dictionary<string, Sprite> Sprites = [];
    private static readonly Dictionary<string, EmbeddedTraitAugment> Rewards = [];

    public static Camera? StereoCamera()
    {
        var camera = Camera.main;
        if (camera == null || !camera.stereoEnabled)
            camera =
                Object.FindObjectsOfType<Camera>().FirstOrDefault(c => c.isActiveAndEnabled && c.stereoEnabled)
                ?? camera;

        return camera;
    }

    public static TMP_FontAsset? MenuFont()
    {
        if (_menuFont != null)
            return _menuFont;

        _menuFont = Resources
            .FindObjectsOfTypeAll<TMP_FontAsset>()
            .FirstOrDefault(font => font != null && font.name == MenuFontName && font.material != null);

        return _menuFont;
    }

    public static ArmamentVisualPreview? GhostStyle() =>
        Find(ref _ghostStyle, preview => preview.unownedShader != null && preview.unownedColors?.Length > 0);

    public static AudioClip? Clip(string name) => FindNamed(Clips, name, clip => clip.name == name);

    public static Sprite? NamedSprite(string name) => FindNamed(Sprites, name, sprite => sprite.name == name);

    public static EmbeddedTraitAugment? Reward(string developerName) =>
        FindNamed(Rewards, developerName, reward => reward.DeveloperName == developerName);

    private static T? FindNamed<T>(Dictionary<string, T> cache, string key, Func<T, bool> match)
        where T : UnityEngine.Object
    {
        if (cache.TryGetValue(key, out var cached) && cached != null)
            return cached;

        var found = Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(item => item != null && match(item));
        if (found != null)
            cache[key] = found;

        return found;
    }

    public static T? Find<T>(ref T? cached, Func<T, bool>? match = null)
        where T : UnityEngine.Object
    {
        if (cached != null)
            return cached;

        cached = Resources
            .FindObjectsOfTypeAll<T>()
            .FirstOrDefault(item => item != null && (match == null || match(item)));

        return cached;
    }
}
