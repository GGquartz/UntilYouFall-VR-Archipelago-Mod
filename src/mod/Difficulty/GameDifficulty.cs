using System.Diagnostics.CodeAnalysis;
using Il2CppSG.Claymore.Level;
using Il2CppSG.GlobalEvents.Variables;
using UnityEngine;

namespace UntilYouFallArchipelago;

internal static class GameDifficulty
{
    private const string Name = "DifficultyLevel";
    private const long RetryDelayMs = 1000;
    private static IntVariable? _cached;
    private static long _retryAt;

    public static bool IsSettingVariable([NotNullWhen(true)] IntVariable? variable) =>
        variable != null && variable.name == Name;

    public static int? Setting()
    {
        if (_cached == null)
        {
            if (Environment.TickCount64 < _retryAt)
                return null;

            SceneCache.Find(ref _cached, IsSettingVariable);
            if (_cached == null)
            {
                _retryAt = Environment.TickCount64 + RetryDelayMs;
                return null;
            }
        }

        return _cached.Value;
    }

    public static int? OfRun(LevelManager? manager) => manager?.campaignDatabase?.Difficulty;
}
