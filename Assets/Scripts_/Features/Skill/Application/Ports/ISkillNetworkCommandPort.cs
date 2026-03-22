using Shared.Kernel;

namespace Features.Skill.Application.Ports
{
    /// <summary>RPC 전송 시 사용하는 네트워크 데이터</summary>
    public readonly struct SkillCastNetworkData
    {
        public DomainEntityId SkillId { get; }
        public DomainEntityId CasterId { get; }
        public float Damage { get; }
        public float Cooldown { get; }
        public float Range { get; }
        public int DeliveryType { get; }
        public int TrajectoryType { get; }
        public int HitType { get; }
        public float Speed { get; }
        public float Radius { get; }
        public float PosX { get; }
        public float PosY { get; }
        public float PosZ { get; }
        public float DirX { get; }
        public float DirY { get; }
        public float DirZ { get; }

        public SkillCastNetworkData(
            DomainEntityId skillId, DomainEntityId casterId,
            float damage, float cooldown, float range,
            int deliveryType,
            int trajectoryType, int hitType, float speed, float radius,
            float posX, float posY, float posZ,
            float dirX, float dirY, float dirZ)
        {
            SkillId = skillId;
            CasterId = casterId;
            Damage = damage;
            Cooldown = cooldown;
            Range = range;
            DeliveryType = deliveryType;
            TrajectoryType = trajectoryType;
            HitType = hitType;
            Speed = speed;
            Radius = radius;
            PosX = posX;
            PosY = posY;
            PosZ = posZ;
            DirX = dirX;
            DirY = dirY;
            DirZ = dirZ;
        }
    }

    public interface ISkillNetworkCommandPort
    {
        void SendSkillCasted(SkillCastNetworkData data);
    }
}
