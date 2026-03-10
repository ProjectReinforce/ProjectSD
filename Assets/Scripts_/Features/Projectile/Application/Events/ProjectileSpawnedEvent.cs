using Shared.Kernel;

namespace Features.Projectile.Application.Events
{
    public readonly struct ProjectileSpawnedEvent
    {
        public ProjectileSpawnedEvent(EntityId projectileId, EntityId ownerId)
        {
            ProjectileId = projectileId;
            OwnerId = ownerId;
        }

        public EntityId ProjectileId { get; }
        public EntityId OwnerId { get; }
    }
}
