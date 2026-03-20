using Features.Skill.Domain;
using Shared.Kernel;

namespace Features.Skill.Application.Events
{
    public readonly struct SkillCastedEvent
    {
        public SkillCastedEvent(DomainEntityId skillId, DomainEntityId casterId, SkillSpec spec)
        {
            SkillId = skillId;
            CasterId = casterId;
            Spec = spec;
        }

        public DomainEntityId SkillId { get; }
        public DomainEntityId CasterId { get; }
        public SkillSpec Spec { get; }
    }
}
