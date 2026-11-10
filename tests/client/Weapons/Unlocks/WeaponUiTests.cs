using UntilYouFallArchipelago;

internal static class WeaponUiTests
{
    public static void Run()
    {
        var failures = new List<string>();
        void Check(bool ok, string message)
        {
            if (!ok)
                failures.Add(message);
        }
        Check(WeaponUiRules.HideSuper(true, true, false, false), "empty AP hand hides a stale super ability");
        Check(WeaponUiRules.HideSuper(true, true, true, true), "a locked ability stays out of the world HUD");
        Check(!WeaponUiRules.HideSuper(true, true, true, false), "an unlocked weapon restores native super UI");
        Check(!WeaponUiRules.HideSuper(false, true, false, true), "without AP the super UI is unchanged");
        Check(
            !WeaponUiRules.HideSuper(true, false, false, true),
            "an uninitialized hand is not treated as an empty loadout"
        );

        var preview = new SavedWeaponPreview();
        preview.Read((IntPtr)1, null);
        preview.Read((IntPtr)2, "starter-guid");
        Check(preview.ShowIcon((IntPtr)1) == false, "saved empty hand stays hidden despite native default import");
        Check(preview.ShowIcon((IntPtr)2) == true, "the other hand retains its saved weapon icon");
        Check(preview.ShowIcon((IntPtr)3) == null, "missing save input leaves new-profile UI to the game");
        preview.Read((IntPtr)1, "unlocked-weapon-guid");
        Check(preview.ShowIcon((IntPtr)1) == true, "a later nonempty import restores the weapon icon");
        preview.Read((IntPtr)2, "");
        Check(preview.ShowIcon((IntPtr)2) == false, "empty serialized string also represents an empty hand");
        var otherProfile = new SavedWeaponPreview();
        Check(otherProfile.ShowIcon((IntPtr)1) == null, "preview state cannot leak into another profile");
        if (failures.Count > 0)
            throw new Exception("Weapon UI: " + string.Join("; ", failures));
        Console.WriteLine("PASS: empty-hand super UI and saved profile icons, fallback import and profile isolation");
    }
}
