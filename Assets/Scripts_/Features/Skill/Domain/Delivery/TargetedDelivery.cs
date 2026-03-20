using Shared.Kernel;

namespace Features.Skill.Domain.Delivery
{
    public sealed class TargetedDelivery : IDeliveryStrategy
    {
        public DeliveryResult Deliver(DomainEntityId skillId, DomainEntityId casterId, SkillSpec spec)
        {
            return new TargetedDeliveryResult();
        }
    }
}
