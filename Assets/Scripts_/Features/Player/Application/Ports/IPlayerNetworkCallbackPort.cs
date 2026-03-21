using Shared.Kernel;

namespace Features.Player.Application.Ports
{
    public interface IPlayerNetworkCallbackPort
    {
        System.Action<DomainEntityId> OnRemoteJumped { set; }
    }
}
