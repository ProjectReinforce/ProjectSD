using Features.Projectile.Domain;

namespace Features.Skill.Domain.Delivery
{
    public abstract class DeliveryResult { }

    public sealed class ProjectileDeliveryResult : DeliveryResult
    {
        public ProjectileSpec ProjectileSpec { get; }

        public ProjectileDeliveryResult(ProjectileSpec projectileSpec)
        {
            ProjectileSpec = projectileSpec;
        }
    }

    public sealed class ZoneDeliveryResult : DeliveryResult { }

    public sealed class TargetedDeliveryResult : DeliveryResult { }

    public sealed class SelfDeliveryResult : DeliveryResult { }
}
