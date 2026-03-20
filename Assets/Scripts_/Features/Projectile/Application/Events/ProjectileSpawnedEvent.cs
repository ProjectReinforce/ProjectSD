using Shared.Kernel;

namespace Features.Projectile.Application.Events
{
    public readonly struct ProjectileSpawnedEvent
    {
        public ProjectileSpawnedEvent(DomainEntityId projectileId, DomainEntityId ownerId)
        {
            ProjectileId = projectileId;
            OwnerId = ownerId;
        }

        public DomainEntityId ProjectileId { get; }
        public DomainEntityId OwnerId { get; }
    }
}
