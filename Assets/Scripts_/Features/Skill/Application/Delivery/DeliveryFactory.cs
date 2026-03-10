using System;
using Features.Skill.Domain;
using Shared.EventBus;

namespace Features.Skill.Application.Delivery
{
    public static class DeliveryFactory
    {
        public static IDeliveryStrategy Create(DeliveryType type, IEventPublisher eventBus)
        {
            switch (type)
            {
                case DeliveryType.Projectile: return new ProjectileDelivery(eventBus);
                case DeliveryType.Zone: return new ZoneDelivery(eventBus);
                case DeliveryType.Targeted: return new TargetedDelivery(eventBus);
                case DeliveryType.Self: return new SelfDelivery(eventBus);
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }
    }
}
