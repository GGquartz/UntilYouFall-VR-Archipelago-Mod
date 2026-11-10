using Il2CppSG.Claymore.Armaments;
using Il2CppSG.Claymore.Entities;
using Il2CppSG.Claymore.Interaction;
using Il2CppSG.Claymore.Level;
using UnityEngine;

namespace UntilYouFallArchipelago;

internal sealed class SuperCharge
{
    private readonly Func<PlayerHealth?> _player;
    private readonly Func<LevelManager?> _level;
    private readonly Func<bool> _ready;
    private readonly WeaponAbilityLocks _locks;
    private readonly LogChannel _log;

    public SuperCharge(
        Func<PlayerHealth?> player,
        Func<LevelManager?> level,
        Func<bool> ready,
        WeaponAbilityLocks locks,
        Action<string> log
    )
    {
        _player = player;
        _level = level;
        _ready = ready;
        _locks = locks;
        _log = log.Channel("APSuperCharge");
    }

    public bool Ready() => !_ready() || Equipped().All(user => !user.isInUse && !user.isActivating);

    public void Apply()
    {
        if (!_ready())
        {
            _log.Write("Not in run, voided");
            return;
        }
        foreach (var user in Equipped())
        {
            if (user.IsCharged || user.isInUse || user.isActivating)
                continue;

            var variables = user.Armament.InstancedVariables;
            var global = variables.GlobalChargeMultiplier.Value;
            var instanced = variables.InstancedChargeMultiplier.Value;
            var amount = SuperChargeRules.Amount(user.MaxCharge, global * instanced);
            if (amount == null)
            {
                _log.Write(
                    $"Invalid charge multiplier guid={user.Armament.ID.GUID} global={global} instanced={instanced} skipped"
                );
                continue;
            }
            var before = user.CurrentCharge;
            if (global * instanced >= 1)
                user.ChargeSuper();
            else
                user.ChargeSuper(amount.Value, true);

            _log.Write(
                $"Native charge guid={user.Armament.ID.GUID} before={before} after={user.CurrentCharge} max={user.MaxCharge} global={global} instanced={instanced} full={user.IsCharged}"
            );
        }
    }

    private IEnumerable<ArmamentAbilityUser> Equipped()
    {
        var player = _player();
        if (player == null)
            yield break;

        var seen = new HashSet<IntPtr>();
        foreach (var hand in _level().ActiveHands())
        {
            if (hand.gameObject.scene != player.gameObject.scene)
                continue;

            var armament = hand.BoundArmament;
            var user = armament?.ability;
            if (
                armament == null
                || user == null
                || armament.BoundHand?.Pointer != hand.Pointer
                || !user.isBoundToHand
                || !user.IsReady
                || user.Ability == null
                || user.chargeVariable == null
                || user.maxChargeVariable == null
                || WeaponCatalog.ByGuid(armament.ID?.GUID ?? "") == null
                || _locks.Locked(armament.ID?.GUID)
                || !seen.Add(armament.Pointer)
            )
                continue;

            yield return user;
        }
    }
}
