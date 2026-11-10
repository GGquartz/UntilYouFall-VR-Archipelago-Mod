using Il2CppSG.Claymore.Armaments;
using Il2CppSG.Claymore.Movement.Dash;
using Il2CppSG.GlobalEvents.Variables;

namespace UntilYouFallArchipelago;

internal sealed class ExhaustionDashRecharge
{
    private FloatVariable? _variable;
    private float _applied;

    public bool Apply(PlayerDash dash)
    {
        var variable = dash._dashChargeRestoreTimeVariable;
        if (variable == null)
            throw new InvalidOperationException("Native dash recharge interval unavailable");
        if (_variable?.Pointer != variable.Pointer)
        {
            Clear();
            _variable = variable;
        }
        var wanted = (variable.Value - _applied) * 2f;
        if (Math.Abs(wanted - _applied) < 0.00001f)
            return false;

        ArmamentStats.ApplyMod(variable, new StatMod { Variable = variable, Amount = wanted - _applied }, 1f);
        _applied = wanted;
        return true;
    }

    public void Clear()
    {
        if (_variable != null && _applied != 0f)
            ArmamentStats.ApplyMod(_variable, new StatMod { Variable = _variable, Amount = -_applied }, 1f);
        _variable = null;
        _applied = 0f;
    }
}
