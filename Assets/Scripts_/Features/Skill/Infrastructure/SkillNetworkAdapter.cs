using Features.Skill.Application.Ports;
using Photon.Pun;
using Shared.Kernel;

namespace Features.Skill.Infrastructure
{
    public sealed class SkillNetworkAdapter : MonoBehaviourPun,
        ISkillNetworkCommandPort, ISkillNetworkCallbackPort
    {
        public System.Action<SkillCastNetworkData> OnRemoteSkillCasted { get; set; }

        public void SendSkillCasted(SkillCastNetworkData data)
        {
            photonView.RPC(nameof(RPC_SkillCasted), RpcTarget.Others,
                data.SkillId.Value, data.CasterId.Value,
                data.DeliveryType,
                new float[]
                {
                    data.Damage, data.Cooldown, data.Range,
                    data.Speed, data.Radius,
                    data.PosX, data.PosY, data.PosZ,
                    data.DirX, data.DirY, data.DirZ
                },
                new int[] { data.TrajectoryType, data.HitType });
        }

        [PunRPC]
        private void RPC_SkillCasted(string skillId, string casterId, int deliveryType, float[] f, int[] i)
        {
            var data = new SkillCastNetworkData(
                new DomainEntityId(skillId), new DomainEntityId(casterId),
                f[0], f[1], f[2],
                deliveryType,
                i[0], i[1], f[3], f[4],
                f[5], f[6], f[7],
                f[8], f[9], f[10]);
            OnRemoteSkillCasted?.Invoke(data);
        }
    }
}
