using Shared.Kernel;

namespace Features.Lobby.Application.Ports
{
    public interface ILobbyRepository
    {
        LobbyState LoadLobby();
        Result SaveLobby(LobbyState lobby);
    }
}
