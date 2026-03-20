using Shared.Kernel;

namespace Features.Projectile.Application.Events
{
    public readonly struct ProjectileHitEvent
    {
        public ProjectileHitEvent(DomainEntityId projectileId, DomainEntityId targetId)
        {
            ProjectileId = projectileId;
            TargetId = targetId;
        }

        public DomainEntityId ProjectileId { get; }
        public DomainEntityId TargetId { get; }
    }
}
