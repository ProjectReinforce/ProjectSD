using Shared.Kernel;

namespace Features.Lobby.Application.Ports
{
    public interface ILobbyMatchNetworkPort
    {
        Result StartGame(EntityId roomId);
    }
}
