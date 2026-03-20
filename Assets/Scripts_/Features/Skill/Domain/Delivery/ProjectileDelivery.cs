using Features.Projectile.Domain;
using Shared.Kernel;

namespace Features.Skill.Domain.Delivery
{
    public sealed class ProjectileDelivery : IDeliveryStrategy
    {
        public ProjectileSpec ProjectileSpec { get; }

        public ProjectileDelivery(ProjectileSpec projectileSpec)
        {
            ProjectileSpec = projectileSpec;
        }

        public DeliveryResult Deliver(DomainEntityId skillId, DomainEntityId casterId, SkillSpec spec)
        {
            return new ProjectileDeliveryResult(ProjectileSpec);
        }
    }
}
