using Shared.Kernel;

namespace Features.Skill.Domain.Delivery
{
    public sealed class TargetedDelivery : IDeliveryStrategy
    {
        public void Deliver(EntityId skillId, EntityId casterId, SkillSpec spec)
        {
            // TODO: 대상 직접 적용 도메인 로직
        }
    }
}
