using Features.Skill.Application.Ports;
using Features.Skill.Domain.Delivery;
using Photon.Pun;
using Shared.Kernel;
using Shared.Math;

namespace Features.Skill.Infrastructure
{
    public sealed class SkillNetworkAdapter : MonoBehaviourPun,
        ISkillNetworkCommandPort, ISkillNetworkCallbackPort
    {
        public System.Action<SkillCastNetworkData> OnSkillCasted { get; set; }

        public void SendSkillCasted(SkillCastNetworkData data)
        {
            photonView.RPC(nameof(RPC_SkillCasted), RpcTarget.All,
                data.SkillId.Value,
                data.CasterId.Value,
                data.SlotIndex,
                (int)data.DeliveryType,
                data.Damage, data.Cooldown, data.Range,
                data.TrajectoryType, data.HitType,
                data.Speed, data.Radius,
                data.Position.X, data.Position.Y, data.Position.Z,
                data.Direction.X, data.Direction.Y, data.Direction.Z);
        }

        [PunRPC]
        private void RPC_SkillCasted(
            string skillId, string casterId,
            int slotIndex, int deliveryType,
            float damage, float cooldown, float range,
            int trajectoryType, int hitType,
            float speed, float radius,
            float posX, float posY, float posZ,
            float dirX, float dirY, float dirZ)
        {
            var data = new SkillCastNetworkData(
                new DomainEntityId(skillId),
                new DomainEntityId(casterId),
                slotIndex,
                damage, cooldown, range,
                (DeliveryType)deliveryType,
                trajectoryType, hitType, speed, radius,
                new Float3(posX, posY, posZ),
                new Float3(dirX, dirY, dirZ));

            OnSkillCasted?.Invoke(data);
        }
    }
}
