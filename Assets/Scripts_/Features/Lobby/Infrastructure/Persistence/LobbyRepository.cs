using Features.Lobby.Application.Ports;
using Shared.Kernel;

using DomainLobby = Features.Lobby.Domain.Lobby;

namespace Features.Lobby.Infrastructure.Persistence
{
    public sealed class LobbyRepository : ILobbyRepository
    {
        private DomainLobby _lobby = new DomainLobby();

        public DomainLobby LoadLobby()
        {
            return _lobby;
        }

        public Result SaveLobby(DomainLobby lobby)
        {
            _lobby = lobby ?? new DomainLobby();
            return Result.Success();
        }
    }
}
