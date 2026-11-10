namespace Il2CppSG.Claymore.Movement.Dash
{
    internal sealed class PlayerDash
    {
        public ChargeVariable _dashChargesVariable = new();
        public float _elapsedDashChargeRestoreTime;
        public GlobalEvents.Variables.FloatVariable _dashChargeRestoreTimeVariable = new();
    }

    internal sealed class ChargeVariable
    {
        private int _value;
        public int Writes;
        public int Value
        {
            get => _value;
            set
            {
                _value = value;
                Writes++;
            }
        }
        public ChargeMaximum Max = new();
    }

    internal sealed class ChargeMaximum
    {
        public int Value;
    }
}

namespace Il2CppSG.GlobalEvents.Variables
{
    internal sealed class FloatVariable
    {
        private static int _nextPointer;
        public IntPtr Pointer { get; } = new(++_nextPointer);
        public float Value = 4f;
        public int Writes;
    }
}

namespace Il2CppSG.Claymore.Armaments
{
    internal sealed class StatMod
    {
        public GlobalEvents.Variables.FloatVariable Variable = null!;
        public float Amount;
    }

    internal static class ArmamentStats
    {
        public static void ApplyMod(GlobalEvents.Variables.FloatVariable variable, StatMod mod, float multiplier)
        {
            variable.Value += mod.Amount * multiplier;
            variable.Writes++;
        }
    }
}
