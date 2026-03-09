using System;
using Features.Lobby.Domain;
using Shared.Kernel;

namespace Features.Lobby.Application.Ports
{
    public interface ILobbyNetworkPort
    {
        Result RequestCreateRoom(Room room, Action onSuccess, Action<string> onFailure);
        Result RequestJoinRoom(EntityId roomId, RoomMember member, Action onSuccess, Action<string> onFailure);
        Result RequestLeaveRoom(EntityId roomId, EntityId memberId, Action onSuccess, Action<string> onFailure);
        Result RequestChangeTeam(EntityId roomId, EntityId memberId, TeamType team, Action onSuccess, Action<string> onFailure);
        Result RequestSetReady(EntityId roomId, EntityId memberId, bool isReady, Action onSuccess, Action<string> onFailure);
        Result RequestStartGame(EntityId roomId);
    }
}
