using Features.Skill.Domain;
using Shared.Kernel;

namespace Features.Skill.Application.Events
{
    public readonly struct SkillCastedEvent
    {
        public SkillCastedEvent(EntityId skillId, EntityId casterId, SkillSpec spec, string deliveryDescription)
        {
            SkillId = skillId;
            CasterId = casterId;
            Spec = spec;
            DeliveryDescription = deliveryDescription;
        }

        public EntityId SkillId { get; }
        public EntityId CasterId { get; }
        public SkillSpec Spec { get; }
        public string DeliveryDescription { get; }
    }
}
