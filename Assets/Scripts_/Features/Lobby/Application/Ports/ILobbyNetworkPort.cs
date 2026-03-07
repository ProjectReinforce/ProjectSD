using Shared.Kernel;

namespace Features.Lobby.Application.Ports
{
    public interface ILobbyNetworkPort
    {
        Result CreateRoom(RoomState room);
        Result JoinRoom(EntityId roomId, MemberState member);
        Result LeaveRoom(EntityId roomId, EntityId memberId);
        Result ChangeTeam(EntityId roomId, EntityId memberId, LobbyTeam team);
        Result SetReady(EntityId roomId, EntityId memberId, bool isReady);
        Result StartGame(EntityId roomId);
    }
}
