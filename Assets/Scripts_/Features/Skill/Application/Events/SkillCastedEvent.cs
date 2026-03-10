using Features.Skill.Domain;
using Shared.Kernel;

namespace Features.Skill.Application.Events
{
    public readonly struct SkillCastedEvent
    {
        public SkillCastedEvent(EntityId skillId, EntityId casterId, SkillSpec spec)
        {
            SkillId = skillId;
            CasterId = casterId;
            Spec = spec;
        }

        public EntityId SkillId { get; }
        public EntityId CasterId { get; }
        public SkillSpec Spec { get; }
    }
}
