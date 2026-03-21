using Shared.Kernel;

namespace Features.Player.Application.Ports
{
    public interface IPlayerNetworkCommandPort
    {
        void SendJump(DomainEntityId playerId);
    }
}
