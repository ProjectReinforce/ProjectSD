using Features.Skill.Domain;
using Shared.Kernel;

namespace Features.Skill.Application.Events
{
    public readonly struct SkillCastedEvent
    {
        public SkillCastedEvent(DomainEntityId skillId, DomainEntityId casterId, int slotIndex, SkillSpec spec)
        {
            SkillId = skillId;
            CasterId = casterId;
            SlotIndex = slotIndex;
            Spec = spec;
        }

        public DomainEntityId SkillId { get; }
        public DomainEntityId CasterId { get; }
        public int SlotIndex { get; }
        public SkillSpec Spec { get; }
    }
}
