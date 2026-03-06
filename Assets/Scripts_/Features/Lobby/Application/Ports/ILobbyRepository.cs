namespace Features.Lobby.Application.Ports
{
    public interface ILobbyRepository
    {
        LobbyState LoadLobby();
        void SaveLobby(LobbyState lobby);
    }
}
