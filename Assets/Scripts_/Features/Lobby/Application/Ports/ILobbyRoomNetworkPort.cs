using Features.Lobby.Domain;
using Shared.Kernel;

namespace Features.Lobby.Application.Ports
{
    public interface ILobbyRoomNetworkPort
    {
        Result CreateRoom(Room room);
        Result JoinRoom(EntityId roomId, RoomMember member);
        Result LeaveRoom(EntityId roomId, EntityId memberId);
    }
}
