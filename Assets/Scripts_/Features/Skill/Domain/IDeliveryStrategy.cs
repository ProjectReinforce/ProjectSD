using Shared.Kernel;

namespace Features.Skill.Domain
{
    public interface IDeliveryStrategy
    {
        void Deliver(EntityId skillId, EntityId casterId, SkillSpec spec);
    }
}
