using Features.Lobby.Domain;
using Shared.Kernel;

namespace Features.Lobby.Application.Ports
{
    public interface ILobbyNetworkCommandPort
    {
        Result CreateRoom(Room room);
        Result JoinRoom(DomainEntityId roomId, RoomMember localMember);
        Result LeaveRoom(DomainEntityId roomId, DomainEntityId memberId);
        Result ChangeTeam(DomainEntityId memberId, TeamType team);
        Result SetReady(DomainEntityId memberId, bool isReady);
        Result StartGame(DomainEntityId roomId);
    }
}
