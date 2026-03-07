using Shared.Kernel;

using DomainLobby = Features.Lobby.Domain.Lobby;

namespace Features.Lobby.Application.Ports
{
    public interface ILobbyRepository
    {
        DomainLobby LoadLobby();
        Result SaveLobby(DomainLobby lobby);
    }
}
