using Features.Lobby.Domain;
using Shared.Kernel;

namespace Features.Lobby.Application.Ports
{
    public interface ILobbyNetworkPort
    {
        Result RequestCreateRoom(Room room);
        Result RequestJoinRoom(EntityId roomId, RoomMember member);
        Result RequestLeaveRoom(EntityId roomId, EntityId memberId);
        Result RequestChangeTeam(EntityId roomId, EntityId memberId, TeamType team);
        Result RequestSetReady(EntityId roomId, EntityId memberId, bool isReady);
        Result RequestStartGame(EntityId roomId);
    }
}
