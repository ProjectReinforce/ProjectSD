using Shared.Kernel;

namespace Features.Combat.Application.Ports
{
    public readonly struct CombatTargetDamageResult
    {
        public CombatTargetDamageResult(float remainingHealth, bool isDead)
        {
            RemainingHealth = remainingHealth;
            IsDead = isDead;
        }

        public float RemainingHealth { get; }
        public bool IsDead { get; }
    }

    public interface ICombatTargetPort
    {
        bool Exists(DomainEntityId targetId);
        float GetDefense(DomainEntityId targetId);
        CombatTargetDamageResult ApplyDamage(DomainEntityId targetId, float damage);
    }
}
