using Shared.Kernel;

namespace Features.Skill.Domain
{
    public sealed class Skill : Entity
    {
        public Skill(EntityId id, SkillSpec spec) : base(id)
        {
            Spec = spec;
        }

        public SkillSpec Spec { get; }
    }
}
