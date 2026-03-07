using DomainLobby = Features.Lobby.Domain.Lobby;
using Room = Features.Lobby.Domain.Room;

namespace Features.Lobby.Application.Ports
{
    public interface ILobbyOutputPort
    {
        void ShowLobby(DomainLobby lobby);
        void ShowRoom(Room room);
        void ShowStartGame(Room room);
        void ShowError(string message);
    }
}
