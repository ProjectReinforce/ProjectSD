using Features.Combat.Domain;
using Shared.Kernel;

namespace Features.Combat.Application.Events
{
    public readonly struct DamageAppliedEvent
    {
        public DamageAppliedEvent(
            DomainEntityId targetId,
            float damage,
            DamageType damageType,
            float remainingHealth,
            bool isDead,
            DomainEntityId attackerId = default
        )
        {
            TargetId = targetId;
            Damage = damage;
            DamageType = damageType;
            RemainingHealth = remainingHealth;
            IsDead = isDead;
            AttackerId = attackerId;
        }

        public DomainEntityId TargetId { get; }
        public float Damage { get; }
        public DamageType DamageType { get; }
        public float RemainingHealth { get; }
        public bool IsDead { get; }
        public DomainEntityId AttackerId { get; }
    }
}
