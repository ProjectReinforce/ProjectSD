using Shared.Kernel;

namespace Features.Skill.Domain.Delivery
{
    public sealed class ZoneDelivery : IDeliveryStrategy
    {
        public DeliveryResult Deliver(DomainEntityId skillId, DomainEntityId casterId, SkillSpec spec)
        {
            return new ZoneDeliveryResult();
        }
    }
}
