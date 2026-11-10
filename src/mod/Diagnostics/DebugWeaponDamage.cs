using HarmonyLib;
using Il2CppSG.Claymore.Armaments;

namespace UntilYouFallArchipelago;

internal static class DebugWeaponDamage
{
    private const int Damage = 99;
    private static readonly List<Armament> Weapons = [];
    private static LogChannel _log = null!;
    private static bool _disabled;

    public static void Install(HarmonyLib.Harmony harmony, bool enabled, Action<string> log)
    {
        _log = log.Channel("DebugDamage");
        if (!enabled)
        {
            _disabled = true;
            _log.Write("Normal weapon damage");
            return;
        }

        harmony.PatchAll(typeof(DebugWeaponDamage));

        _log.Write("Enabled weapon damage HealthDamage=99 GuardDamage=99");
    }

    [HarmonyPatch(typeof(Armament), nameof(Armament.BindToHand))]
    [HarmonyPostfix]
    private static void OnBound(Armament __instance)
    {
        if (!Weapons.Any(weapon => weapon.Pointer == __instance.Pointer))
            Weapons.Add(__instance);

        Update();
    }

    public static void Update()
    {
        if (_disabled)
            return;

        try
        {
            for (var i = Weapons.Count - 1; i >= 0; i--)
            {
                var weapon = Weapons[i];
                if (weapon == null || weapon.BoundHand == null)
                {
                    Weapons.RemoveAt(i);
                    continue;
                }
                if (!weapon.IsReady)
                    continue;
                var variables = weapon.InstancedVariables;
                if (variables == null || variables.HealthDamage == null || variables.GuardDamage == null)
                    continue;
                if (variables.HealthDamage.Value == Damage && variables.GuardDamage.Value == Damage)
                    continue;

                var beforeHealth = variables.HealthDamage.Value;
                var beforeGuard = variables.GuardDamage.Value;
                variables.HealthDamage.Value = Damage;
                variables.GuardDamage.Value = Damage;

                _log.Write($"weapon={weapon.name} HealthDamage={beforeHealth}->99 GuardDamage={beforeGuard}->99");
            }
        }
        catch (Exception ex)
        {
            _disabled = true;
            _log.Write("Disabled, error: " + ex.Message);
        }
    }
}
