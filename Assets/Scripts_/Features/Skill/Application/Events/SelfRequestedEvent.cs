using Features.Skill.Domain;
using Shared.Kernel;
using Shared.Math;

namespace Features.Skill.Application.Events
{
    public readonly struct SelfRequestedEvent
    {
        public SelfRequestedEvent(DomainEntityId casterId, SkillSpec spec, Float3 position)
        {
            CasterId = casterId;
            Spec = spec;
            Position = position;
        }

        public DomainEntityId CasterId { get; }
        public SkillSpec Spec { get; }
        public Float3 Position { get; }
    }
}
