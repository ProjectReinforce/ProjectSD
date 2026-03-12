using Shared.Kernel;

namespace Features.Skill.Domain.Delivery
{
    public sealed class ZoneDelivery : IDeliveryStrategy
    {
        public DeliveryResult Deliver(EntityId skillId, EntityId casterId, SkillSpec spec)
        {
            return new ZoneDeliveryResult();
        }
    }
}
