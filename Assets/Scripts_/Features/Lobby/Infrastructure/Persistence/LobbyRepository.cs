using Features.Lobby.Application;
using Features.Lobby.Application.Ports;
using Shared.Kernel;

namespace Features.Lobby.Infrastructure.Persistence
{
    public sealed class LobbyRepository : ILobbyRepository
    {
        private LobbyState _lobby = LobbyState.Empty;

        public LobbyState LoadLobby()
        {
            return _lobby;
        }

        public Result SaveLobby(LobbyState lobby)
        {
            _lobby = lobby ?? LobbyState.Empty;
            return Result.Success();
        }
    }
}
