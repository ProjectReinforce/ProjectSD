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

        public DeliveryResult Deliver(EntityId skillId, EntityId casterId, SkillSpec spec)
        {
            return new DeliveryResult(
                $"[ProjectileDelivery] skill={skillId} caster={casterId} dmg={spec.Damage} range={spec.Range}" +
                $" trajectory={ProjectileSpec.TrajectoryType} hit={ProjectileSpec.HitType}" +
                $" speed={ProjectileSpec.Speed} radius={ProjectileSpec.Radius}");
        }
    }
}
