using Features.Projectile.Domain;
using Shared.Kernel;
using Shared.Math;

namespace Features.Projectile.Application.Events
{
    public readonly struct ProjectileRequestedEvent
    {
        public ProjectileRequestedEvent(DomainEntityId ownerId, ProjectileSpec spec, Float3 position, Float3 direction)
        {
            OwnerId = ownerId;
            Spec = spec;
            Position = position;
            Direction = direction;
        }

        public DomainEntityId OwnerId { get; }
        public ProjectileSpec Spec { get; }
        public Float3 Position { get; }
        public Float3 Direction { get; }
    }
}
