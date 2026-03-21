using Shared.Kernel;

namespace Features.Skill.Application.Ports
{
    public interface ISkillNetworkCallbackPort
    {
        System.Action<DomainEntityId, DomainEntityId, float, float, float> OnRemoteSkillCasted { set; }
    }
}
