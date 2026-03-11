using Shared.Kernel;

namespace Features.Skill.Domain.Delivery
{
    public sealed class ZoneDelivery : IDeliveryStrategy
    {
        public void Deliver(EntityId skillId, EntityId casterId, SkillSpec spec)
        {
            // TODO: 범위 계산, 영역 판정 규칙 등 도메인 로직
        }
    }
}
