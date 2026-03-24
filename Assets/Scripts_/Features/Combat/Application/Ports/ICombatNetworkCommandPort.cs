using Features.Combat.Domain;
using Shared.Kernel;

namespace Features.Combat.Application.Ports
{
    public interface ICombatNetworkCommandPort
    {
        void SendDamage(DomainEntityId targetId, float damage, DamageType damageType, DomainEntityId attackerId);
        void SendDeath(DomainEntityId targetId, DomainEntityId killerId);
        void SendRespawn(DomainEntityId targetId);
    }
}
