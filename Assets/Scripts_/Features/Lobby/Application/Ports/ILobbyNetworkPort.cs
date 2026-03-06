using Shared.Kernel;

namespace Features.Lobby.Application.Ports
{
    public interface ILobbyNetworkPort
    {
        Result CreateRoom(RoomState room);
        Result JoinRoom(EntityId roomId, MemberState member);
        Result LeaveRoom(EntityId roomId, EntityId memberId);
        Result StartGame(EntityId roomId);
    }
}
