namespace Features.Lobby.Application.Ports
{
    public interface ILobbyOutputPort
    {
        void ShowLobby(LobbyState lobby);
        void ShowRoom(RoomState room);
        void ShowStartGame(RoomState room);
        void ShowError(string message);
    }
}
