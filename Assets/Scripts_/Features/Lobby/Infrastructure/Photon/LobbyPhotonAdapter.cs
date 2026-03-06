using Features.Lobby.Application.Ports;
using Features.Lobby.Application;
using Shared.Kernel;
using UnityEngine;

namespace Features.Lobby.Infrastructure.Photon
{
    public sealed class LobbyPhotonAdapter : ILobbyNetworkPort
    {
        public Result CreateRoom(RoomState room)
        {
            Debug.Log("[LobbyPhotonAdapter] CreateRoom: " + room.Name);
            return Result.Success();
        }

        public Result JoinRoom(EntityId roomId, MemberState member)
        {
            Debug.Log("[LobbyPhotonAdapter] JoinRoom: " + roomId + ", Member=" + member.DisplayName);
            return Result.Success();
        }

        public Result LeaveRoom(EntityId roomId, EntityId memberId)
        {
            Debug.Log("[LobbyPhotonAdapter] LeaveRoom: " + roomId + ", Member=" + memberId);
            return Result.Success();
        }

        public Result StartGame(EntityId roomId)
        {
            Debug.Log("[LobbyPhotonAdapter] StartGame: " + roomId);
            return Result.Success();
        }
    }
}
