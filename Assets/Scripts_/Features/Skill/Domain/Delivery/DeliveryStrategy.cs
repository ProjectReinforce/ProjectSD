using Shared.Kernel;

namespace Features.Skill.Domain.Delivery
{
    public interface IDeliveryStrategy
    {
        DeliveryResult Deliver(DomainEntityId skillId, DomainEntityId casterId, SkillSpec spec);
    }
}
