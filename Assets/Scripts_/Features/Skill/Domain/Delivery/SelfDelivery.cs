using Shared.Kernel;

namespace Features.Skill.Domain.Delivery
{
    public sealed class SelfDelivery : IDeliveryStrategy
    {
        public DeliveryResult Deliver(DomainEntityId skillId, DomainEntityId casterId, SkillSpec spec)
        {
            return new SelfDeliveryResult();
        }
    }
}
