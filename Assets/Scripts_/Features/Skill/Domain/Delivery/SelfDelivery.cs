using Shared.Kernel;

namespace Features.Skill.Domain.Delivery
{
    public sealed class SelfDelivery : IDeliveryStrategy
    {
        public void Deliver(EntityId skillId, EntityId casterId, SkillSpec spec)
        {
            // TODO: 자기 자신 적용 도메인 로직
        }
    }
}
