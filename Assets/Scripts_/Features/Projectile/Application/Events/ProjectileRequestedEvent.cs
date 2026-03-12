using Features.Projectile.Domain;
using Shared.Kernel;

namespace Features.Projectile.Application.Events
{
    public readonly struct ProjectileRequestedEvent
    {
        public ProjectileRequestedEvent(EntityId ownerId, ProjectileSpec spec)
        {
            OwnerId = ownerId;
            Spec = spec;
        }

        public EntityId OwnerId { get; }
        public ProjectileSpec Spec { get; }
    }
}
