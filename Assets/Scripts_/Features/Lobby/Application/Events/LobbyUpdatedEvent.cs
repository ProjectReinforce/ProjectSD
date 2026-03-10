using DomainLobby = Features.Lobby.Domain.Lobby;

namespace Features.Lobby.Application.Events
{
    public readonly struct LobbyUpdatedEvent
    {
        public LobbyUpdatedEvent(DomainLobby lobby) => Lobby = new LobbySnapshot(lobby);
        public LobbySnapshot Lobby { get; }
    }
}
