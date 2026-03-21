using Features.Skill.Domain;
using Shared.Kernel;

namespace Features.Skill.Application.Events
{
    public readonly struct TargetedRequestedEvent
    {
        public TargetedRequestedEvent(DomainEntityId casterId, SkillSpec spec)
        {
            CasterId = casterId;
            Spec = spec;
        }

        public DomainEntityId CasterId { get; }
        public SkillSpec Spec { get; }
    }
}
