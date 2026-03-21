using Features.Skill.Application.Ports;
using Photon.Pun;
using Shared.Kernel;

namespace Features.Skill.Infrastructure
{
    public sealed class SkillNetworkAdapter : MonoBehaviourPun,
        ISkillNetworkCommandPort, ISkillNetworkCallbackPort
    {
        // ISkillNetworkCallbackPort
        public System.Action<DomainEntityId, DomainEntityId, float, float, float> OnRemoteSkillCasted { get; set; }

        // ISkillNetworkCommandPort
        public void SendSkillCasted(DomainEntityId skillId, DomainEntityId casterId, float damage, float cooldown, float range)
        {
            photonView.RPC(nameof(RPC_SkillCasted), RpcTarget.Others,
                skillId.Value, casterId.Value, damage, cooldown, range);
        }

        [PunRPC]
        private void RPC_SkillCasted(string skillIdValue, string casterIdValue, float damage, float cooldown, float range)
        {
            var skillId = new DomainEntityId(skillIdValue);
            var casterId = new DomainEntityId(casterIdValue);
            OnRemoteSkillCasted?.Invoke(skillId, casterId, damage, cooldown, range);
        }
    }
}
