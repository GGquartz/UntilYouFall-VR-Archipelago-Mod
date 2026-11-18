namespace Il2Cpp
{
    public class NamespaceMarker { }
}

namespace Il2CppSG.Claymore.Combat.Combatants
{
    public class NPCCombatantDefinition
    {
        public string GUID = "";
        public string DeveloperName = "test enemy";

        public T? TryCast<T>()
            where T : class => this as T;
    }
}

namespace Il2CppSG.Claymore.NPCs
{
    public class Enemy
    {
        public Combat.Combatants.NPCCombatantDefinition? EntityDefinition;
    }
}

namespace Il2CppSG.Claymore.Player
{
    public class PlayerDeathController
    {
        public void OnPlayerKilled(NPCs.Enemy? killer) { }
    }
}

namespace Il2CppSG.Claymore.Entities
{
    public class PlayerHealth
    {
        public float CurrentHealth = 8;
        public bool IsOnDeathsDoor;
        public bool IsDead;
        public bool IsInvulnerable;
        public float timeLastHit;
        public float timeEnteredInvulnerability;
        public int DeathsDoorEntries;
        public Action? OnKilled;

        public void KillPlayer() => OnKilled?.Invoke();

        public void Set(float value) => CurrentHealth = value;

        public void DealMassiveDamage()
        {
            DeathsDoorEntries++;
            CurrentHealth = 0;
            IsOnDeathsDoor = true;
        }
    }
}

namespace Il2CppSG.Claymore.UI
{
    public class PauseScreen
    {
        public void HandleGiveUpButton() { }

        public void HandleQuitToTitleButton() { }
    }
}
