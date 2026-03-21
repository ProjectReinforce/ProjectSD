using Features.Skill.Domain;
using Shared.Kernel;

namespace Features.Skill.Application.Events
{
    public readonly struct SkillEquippedEvent
    {
        public SkillEquippedEvent(int slotIndex, DomainEntityId skillId, SkillSpec spec)
        {
            SlotIndex = slotIndex;
            SkillId = skillId;
            Spec = spec;
        }

        public int SlotIndex { get; }
        public DomainEntityId SkillId { get; }
        public SkillSpec Spec { get; }
    }
}
