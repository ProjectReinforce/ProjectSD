using Shared.Kernel;

namespace Features.Skill.Application.Ports
{
    public interface ISkillNetworkCommandPort
    {
        void SendSkillCasted(DomainEntityId skillId, DomainEntityId casterId, float damage, float cooldown, float range);
    }
}
