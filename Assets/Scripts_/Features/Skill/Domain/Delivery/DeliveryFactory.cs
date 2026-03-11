using System;

namespace Features.Skill.Domain.Delivery
{
    public static class DeliveryFactory
    {
        public static IDeliveryStrategy Create(DeliveryType type)
        {
            switch (type)
            {
                case DeliveryType.Projectile: return new ProjectileDelivery();
                case DeliveryType.Zone: return new ZoneDelivery();
                case DeliveryType.Targeted: return new TargetedDelivery();
                case DeliveryType.Self: return new SelfDelivery();
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }
    }
}
