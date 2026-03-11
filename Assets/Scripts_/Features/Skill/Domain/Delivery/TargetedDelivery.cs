using Shared.Kernel;

namespace Features.Skill.Domain.Delivery
{
    public sealed class TargetedDelivery : IDeliveryStrategy
    {
        public DeliveryResult Deliver(EntityId skillId, EntityId casterId, SkillSpec spec)
        {
            return new DeliveryResult(
                $"[TargetedDelivery] skill={skillId} caster={casterId} dmg={spec.Damage} range={spec.Range}");
        }
    }
}
