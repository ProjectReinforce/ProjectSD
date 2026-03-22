using Features.Projectile.Domain;

namespace Features.Skill.Domain.Delivery
{
    public enum DeliveryType
    {
        Projectile = 0,
        Zone = 1,
        Targeted = 2,
        Self = 3
    }

    public abstract class DeliveryResult
    {
        public abstract DeliveryType DeliveryType { get; }
    }

    public sealed class ProjectileDeliveryResult : DeliveryResult
    {
        public override DeliveryType DeliveryType => DeliveryType.Projectile;
        public ProjectileSpec ProjectileSpec { get; }

        public ProjectileDeliveryResult(ProjectileSpec projectileSpec)
        {
            ProjectileSpec = projectileSpec;
        }
    }

    public sealed class ZoneDeliveryResult : DeliveryResult
    {
        public override DeliveryType DeliveryType => DeliveryType.Zone;
    }

    public sealed class TargetedDeliveryResult : DeliveryResult
    {
        public override DeliveryType DeliveryType => DeliveryType.Targeted;
    }

    public sealed class SelfDeliveryResult : DeliveryResult
    {
        public override DeliveryType DeliveryType => DeliveryType.Self;
    }
}
