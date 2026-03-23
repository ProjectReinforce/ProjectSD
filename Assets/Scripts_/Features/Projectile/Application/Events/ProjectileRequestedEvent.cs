using Features.Combat.Domain;
using Features.Projectile.Domain;
using Shared.Kernel;
using Shared.Math;

namespace Features.Projectile.Application.Events
{
    public readonly struct ProjectileRequestedEvent
    {
        public ProjectileRequestedEvent(
            DomainEntityId ownerId,
            ProjectileSpec spec,
            float baseDamage,
            DamageType damageType,
            Float3 position,
            Float3 direction
        )
        {
            OwnerId = ownerId;
            Spec = spec;
            BaseDamage = baseDamage;
            DamageType = damageType;
            Position = position;
            Direction = direction;
        }

        public DomainEntityId OwnerId { get; }
        public ProjectileSpec Spec { get; }
        public float BaseDamage { get; }
        public DamageType DamageType { get; }
        public Float3 Position { get; }
        public Float3 Direction { get; }
    }
}
