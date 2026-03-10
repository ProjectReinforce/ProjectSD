using Shared.Kernel;

namespace Features.Projectile.Application.Events
{
    public readonly struct ProjectileHitEvent
    {
        public ProjectileHitEvent(EntityId projectileId, EntityId targetId)
        {
            ProjectileId = projectileId;
            TargetId = targetId;
        }

        public EntityId ProjectileId { get; }
        public EntityId TargetId { get; }
    }
}
