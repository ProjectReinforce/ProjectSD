using Features.Projectile.Domain;
using Shared.Kernel;

namespace Features.Projectile.Application.Events
{
    public readonly struct ProjectileRequestedEvent
    {
        public ProjectileRequestedEvent(DomainEntityId ownerId, ProjectileSpec spec)
        {
            OwnerId = ownerId;
            Spec = spec;
        }

        public DomainEntityId OwnerId { get; }
        public ProjectileSpec Spec { get; }
    }
}
