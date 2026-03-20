using Features.Skill.Domain.Delivery;
using Shared.Kernel;

namespace Features.Skill.Domain
{
    public sealed class Skill : Entity
    {
        public Skill(DomainEntityId id, SkillSpec spec, IDeliveryStrategy delivery) : base(id)
        {
            Spec = spec;
            Delivery = delivery;
        }

        public SkillSpec Spec { get; }
        public IDeliveryStrategy Delivery { get; }
    }
}
