namespace UntilYouFallArchipelago;

internal static class WeaponUiRules
{
    public static bool HideSuper(bool apActive, bool hasHandState, bool hasWeapon, bool abilityLocked) =>
        apActive && hasHandState && (!hasWeapon || abilityLocked);
}

internal sealed class SavedWeaponPreview
{
    private readonly Dictionary<IntPtr, bool> _icons = [];

    public void Read(IntPtr hand, string? savedWeapon) => _icons[hand] = !string.IsNullOrEmpty(savedWeapon);

    public bool? ShowIcon(IntPtr hand) => _icons.TryGetValue(hand, out var show) ? show : null;
}
