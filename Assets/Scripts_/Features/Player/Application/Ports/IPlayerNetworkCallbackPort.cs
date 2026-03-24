using Features.Combat.Domain;
using Shared.Kernel;

namespace Features.Player.Application.Ports
{
    public interface IPlayerNetworkCallbackPort
    {
        System.Action<DomainEntityId> OnRemoteJumped { set; }
        System.Action<DomainEntityId, float, DamageType, DomainEntityId> OnRemoteDamaged { set; }
        System.Action<DomainEntityId, DomainEntityId> OnRemoteDied { set; }
        System.Action<DomainEntityId> OnRemoteRespawned { set; }
        System.Action<DomainEntityId, float, float> OnHealthSynced { set; }
    }
}
