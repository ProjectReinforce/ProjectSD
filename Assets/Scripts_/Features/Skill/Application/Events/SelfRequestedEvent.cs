using Features.Skill.Domain;
using Shared.Kernel;
using Shared.Math;

namespace Features.Skill.Application.Events
{
    public readonly struct SelfRequestedEvent
    {
        public SelfRequestedEvent(DomainEntityId skillId, DomainEntityId casterId, SkillSpec spec, Float3 position)
        {
            SkillId = skillId;
            CasterId = casterId;
            Spec = spec;
            Position = position;
        }

        public DomainEntityId SkillId { get; }
        public DomainEntityId CasterId { get; }
        public SkillSpec Spec { get; }
        public Float3 Position { get; }
    }
}
