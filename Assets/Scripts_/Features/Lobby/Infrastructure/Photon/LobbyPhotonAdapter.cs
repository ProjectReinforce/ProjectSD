using Features.Lobby.Application;
using Features.Lobby.Application.Ports;
using UnityEngine;
using EntityId = Shared.Kernel.EntityId;
using Result = Shared.Kernel.Result;

namespace Features.Lobby.Infrastructure.Photon
{
    public sealed class LobbyPhotonAdapter : ILobbyNetworkPort
    {
        public Result CreateRoom(RoomState room)
        {
            Debug.Log($"[LobbyPhotonAdapter] CreateRoom: {room.Name}");
            return Result.Success();
        }

        public Result JoinRoom(EntityId roomId, MemberState member)
        {
            Debug.Log($"[LobbyPhotonAdapter] JoinRoom: {roomId}, Member={member.DisplayName}");
            return Result.Success();
        }

        public Result LeaveRoom(EntityId roomId, EntityId memberId)
        {
            Debug.Log($"[LobbyPhotonAdapter] LeaveRoom: {roomId}, Member={memberId}");
            return Result.Success();
        }

        public Result ChangeTeam(EntityId roomId, EntityId memberId, LobbyTeam team)
        {
            Debug.Log($"[LobbyPhotonAdapter] ChangeTeam: {roomId}, Member={memberId}, Team={team}");
            return Result.Success();
        }

        public Result SetReady(EntityId roomId, EntityId memberId, bool isReady)
        {
            Debug.Log($"[LobbyPhotonAdapter] SetReady: {roomId}, Member={memberId}, IsReady={isReady}");
            return Result.Success();
        }

        public Result StartGame(EntityId roomId)
        {
            Debug.Log($"[LobbyPhotonAdapter] StartGame: {roomId}");
            return Result.Success();
        }
    }
}
