using Features.Combat.Domain;
using Shared.Kernel;

namespace Features.Combat.Application.Events
{
    public readonly struct DamageAppliedEvent
    {
        public DamageAppliedEvent(EntityId targetId, float damage, DamageType damageType)
        {
            TargetId = targetId;
            Damage = damage;
            DamageType = damageType;
        }

        public EntityId TargetId { get; }
        public float Damage { get; }
        public DamageType DamageType { get; }
    }
}
