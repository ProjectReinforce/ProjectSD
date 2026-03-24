using Shared.Kernel;

namespace Features.Combat.Domain
{
    public sealed class CombatTarget : Entity
    {
        public CombatTarget(DomainEntityId id, float maxHealth, float currentHealth, float defense) : base(id)
        {
            MaxHealth = maxHealth > 0f ? maxHealth : 1f;
            CurrentHealth = currentHealth < 0f
                ? 0f
                : currentHealth > MaxHealth ? MaxHealth : currentHealth;
            Defense = defense < 0f ? 0f : defense;
        }

        public float MaxHealth { get; }
        public float CurrentHealth { get; private set; }
        public float Defense { get; }
        public bool IsDead => CurrentHealth <= 0f;

        public float ApplyDamage(float damage)
        {
            if (damage < 0f)
                damage = 0f;

            CurrentHealth -= damage;
            if (CurrentHealth < 0f)
                CurrentHealth = 0f;

            return CurrentHealth;
        }

        public float Reset()
        {
            CurrentHealth = MaxHealth;
            return CurrentHealth;
        }
    }
}
