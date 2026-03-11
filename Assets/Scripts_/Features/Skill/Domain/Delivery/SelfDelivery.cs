using Shared.Kernel;

namespace Features.Skill.Domain.Delivery
{
    public sealed class SelfDelivery : IDeliveryStrategy
    {
        public DeliveryResult Deliver(EntityId skillId, EntityId casterId, SkillSpec spec)
        {
            return new DeliveryResult(
                $"[SelfDelivery] skill={skillId} caster={casterId} dmg={spec.Damage} range={spec.Range}");
        }
    }
}
