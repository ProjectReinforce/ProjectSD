using Features.Lobby.Domain;
using Shared.Kernel;

namespace Features.Lobby.Application.Ports
{
    public interface ILobbyNetworkPort
    {
        Result CreateRoom(Room room);
        Result JoinRoom(EntityId roomId, RoomMember member);
        Result LeaveRoom(EntityId roomId, EntityId memberId);
        Result ChangeTeam(EntityId roomId, EntityId memberId, TeamType team);
        Result SetReady(EntityId roomId, EntityId memberId, bool isReady);
        Result StartGame(EntityId roomId);
    }
}
