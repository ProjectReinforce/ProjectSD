using Shared.Kernel;

namespace Features.Skill.Domain.Delivery
{
    public interface IDeliveryStrategy
    {
        DeliveryResult Deliver(EntityId skillId, EntityId casterId, SkillSpec spec);
    }
}
