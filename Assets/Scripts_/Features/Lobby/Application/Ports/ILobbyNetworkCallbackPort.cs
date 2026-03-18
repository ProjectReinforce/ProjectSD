using System;
using System.Collections.Generic;
using Features.Lobby.Domain;
using Shared.Kernel;

namespace Features.Lobby.Application.Ports
{
    /// <summary>
    /// Photon 방 참가 성공 콜백에서 전달되는 데이터 묶음.
    /// </summary>
    public sealed class JoinRoomData
    {
        public EntityId RoomId { get; }
        public string RoomName { get; }
        public int Capacity { get; }
        public List<RoomMember> Members { get; }
        public EntityId MasterMemberId { get; }
        public EntityId LocalMemberId { get; }

        public JoinRoomData(EntityId roomId, string roomName, int capacity,
            List<RoomMember> members, EntityId masterMemberId, EntityId localMemberId)
        {
            RoomId = roomId;
            RoomName = roomName;
            Capacity = capacity;
            Members = members;
            MasterMemberId = masterMemberId;
            LocalMemberId = localMemberId;
        }
    }

    /// <summary>
    /// Photon 플레이어 속성 변경 콜백에서 전달되는 데이터 묶음.
    /// </summary>
    public sealed class PlayerPropertiesData
    {
        public EntityId RoomId { get; }
        public EntityId MemberId { get; }
        public TeamType? Team { get; }
        public bool? IsReady { get; }

        public PlayerPropertiesData(EntityId roomId, EntityId memberId, TeamType? team, bool? isReady)
        {
            RoomId = roomId;
            MemberId = memberId;
            Team = team;
            IsReady = isReady;
        }
    }

    /// <summary>
    /// Photon 로비 방 목록에서 전달되는 개별 방 정보.
    /// </summary>
    public sealed class RoomListItem
    {
        public EntityId RoomId { get; }
        public string RoomName { get; }
        public int PlayerCount { get; }
        public int MaxPlayers { get; }
        public bool IsOpen { get; }

        public RoomListItem(EntityId roomId, string roomName, int playerCount, int maxPlayers, bool isOpen)
        {
            RoomId = roomId;
            RoomName = roomName;
            PlayerCount = playerCount;
            MaxPlayers = maxPlayers;
            IsOpen = isOpen;
        }
    }

    public interface ILobbyNetworkCallbackPort
    {
        Action<Room> OnCreateRoomSucceeded { set; }
        Action<string> OnErrorOccurred { set; }
        Action<JoinRoomData> OnJoinRoomSucceeded { set; }
        Action<EntityId, EntityId> OnLeaveRoomSucceeded { set; }
        Action<EntityId, RoomMember> OnRemotePlayerEntered { set; }
        Action<EntityId, EntityId> OnRemotePlayerLeft { set; }
        Action<PlayerPropertiesData> OnPlayerPropertiesChanged { set; }
        Action<EntityId> OnGameStarted { set; }
        Action<List<RoomListItem>> OnRoomListUpdated { set; }
    }
}
