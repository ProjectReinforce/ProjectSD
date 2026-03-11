using Shared.Kernel;

namespace Features.Skill.Domain.Delivery
{
    public sealed class ProjectileDelivery : IDeliveryStrategy
    {
        public void Deliver(EntityId skillId, EntityId casterId, SkillSpec spec)
        {
            // TODO: 탄도 계산, 투사체 판정 규칙 등 도메인 로직
        }
    }
}
