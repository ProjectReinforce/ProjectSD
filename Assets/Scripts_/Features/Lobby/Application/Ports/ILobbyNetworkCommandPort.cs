using Features.Lobby.Domain;
using Shared.Kernel;

namespace Features.Lobby.Application.Ports
{
    public interface ILobbyNetworkCommandPort
    {
        Result CreateRoom(Room room);
        Result JoinRoom(EntityId roomId, RoomMember localMember);
        Result LeaveRoom(EntityId roomId, EntityId memberId);
        Result ChangeTeam(EntityId memberId, TeamType team);
        Result SetReady(EntityId memberId, bool isReady);
        Result StartGame(EntityId roomId);
    }
}
