using Features.Lobby.Application.Ports;
using Features.Lobby.Application;

namespace Features.Lobby.Infrastructure.Persistence
{
    public sealed class LobbyRepository : ILobbyRepository
    {
        private LobbyState _lobby = LobbyState.Empty;

        public LobbyState LoadLobby()
        {
            return _lobby;
        }

        public void SaveLobby(LobbyState lobby)
        {
            _lobby = lobby ?? LobbyState.Empty;
        }
    }
}
