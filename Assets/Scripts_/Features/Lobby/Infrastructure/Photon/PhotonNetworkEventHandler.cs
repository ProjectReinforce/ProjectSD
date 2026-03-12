using System.Collections.Generic;
using ExitGames.Client.Photon;
using Features.Lobby.Application.Handlers;
using Features.Lobby.Domain;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using EntityId = Shared.Kernel.EntityId;

using DomainRoom = Features.Lobby.Domain.Room;
using PhotonRoom = Photon.Realtime.Room;

namespace Features.Lobby.Infrastructure.Photon
{
    public sealed class PhotonNetworkEventHandler : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        private LobbyStateSyncHandler _syncHandler;
        
        // Pending state (data only, no callbacks)
        private DomainRoom _pendingCreateRoom;
        private bool _pendingJoin;
        private EntityId _pendingLeaveRoomId;
        private EntityId _pendingLeaveMemberId;

        internal void Initialize(LobbyStateSyncHandler syncHandler)
        {
            _syncHandler = syncHandler;
        }

        internal void SetPendingCreate(DomainRoom room)
        {
            _pendingCreateRoom = room;
            _pendingJoin = false;
        }

        internal void SetPendingJoin()
        {
            _pendingCreateRoom = null;
            _pendingJoin = true;
        }

        internal void SetPendingLeave(EntityId roomId, EntityId memberId)
        {
            _pendingLeaveRoomId = roomId;
            _pendingLeaveMemberId = memberId;
        }

        internal void ClearPending()
        {
            _pendingCreateRoom = null;
            _pendingJoin = false;
            _pendingLeaveRoomId = default;
            _pendingLeaveMemberId = default;
        }

        // --- Room Creation Callbacks ---

        public override void OnCreatedRoom()
        {
            var room = _pendingCreateRoom;
            _pendingCreateRoom = null;

            if (room == null)
            {
                Debug.LogWarning("[PhotonEventHandler] Unexpected OnCreatedRoom: no pending create.");
                return;
            }

            var result = _syncHandler.HandleCreateRoomSucceeded(room);
            if (result.IsFailure)
            {
                Debug.LogError($"[PhotonEventHandler] OnCreatedRoom: {result.Error}");
            }
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            _pendingCreateRoom = null;
            _syncHandler.PublishError($"Create room failed ({returnCode}): {message}");
        }

        // --- Room Join Callbacks ---

        public override void OnJoinedRoom()
        {
            // Creator also receives OnJoinedRoom after OnCreatedRoom — skip it
            if (!_pendingJoin) return;
            _pendingJoin = false;

            var photonRoom = PhotonNetwork.CurrentRoom;
            var roomId = new EntityId(photonRoom.Name);
            var roomName = photonRoom.CustomProperties.TryGetValue(LobbyPhotonConstants.RoomDisplayNameKey, out var nameRaw) && nameRaw is string nameStr
                ? nameStr : photonRoom.Name;

            var members = BuildMembersFromPlayers(photonRoom);
            if (members.Count == 0)
            {
                Debug.LogError("[PhotonEventHandler] OnJoinedRoom: no members could be built from players.");
                return;
            }

            EntityId masterMemberId = default;
            if (photonRoom.Players.TryGetValue(photonRoom.MasterClientId, out var masterPlayer))
            {
                var m = BuildMemberFromPlayer(masterPlayer);
                if (m != null)
                    masterMemberId = m.Id;
            }

            var result = _syncHandler.HandleJoinRoomSucceeded(
                roomId, roomName, photonRoom.MaxPlayers, members, masterMemberId);
            if (result.IsFailure)
            {
                Debug.LogError($"[PhotonEventHandler] OnJoinedRoom: {result.Error}");
            }
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            _pendingJoin = false;
            _syncHandler.PublishError($"Join room failed ({returnCode}): {message}");
        }

        // --- Room Leave Callbacks ---

        public override void OnLeftRoom()
        {
            var roomId = _pendingLeaveRoomId;
            var memberId = _pendingLeaveMemberId;
            _pendingLeaveRoomId = default;
            _pendingLeaveMemberId = default;

            if (string.IsNullOrWhiteSpace(roomId.Value))
            {
                Debug.LogWarning("[PhotonEventHandler] Unexpected OnLeftRoom: no pending leave info.");
                return;
            }

            var result = _syncHandler.HandleLeaveRoomSucceeded(roomId, memberId);
            if (result.IsFailure)
                Debug.LogWarning($"[PhotonEventHandler] OnLeftRoom: {result.Error}");
        }

        // --- Remote Player Join/Leave Callbacks ---

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            if (!PhotonNetwork.InRoom) return;
            if (newPlayer == PhotonNetwork.LocalPlayer) return;

            var member = BuildMemberFromPlayer(newPlayer);
            if (member == null)
            {
                Debug.LogWarning("[PhotonEventHandler] Remote player entered but has no memberId property.");
                return;
            }

            var roomId = new EntityId(PhotonNetwork.CurrentRoom.Name);
            var result = _syncHandler.HandleRemotePlayerEntered(roomId, member);
            if (result.IsFailure)
            {
                Debug.LogWarning($"[PhotonEventHandler] Failed to add remote player: {result.Error}");
            }
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            if (!PhotonNetwork.InRoom) return;
            if (otherPlayer == PhotonNetwork.LocalPlayer) return;

            if (!otherPlayer.CustomProperties.TryGetValue(LobbyPhotonConstants.MemberIdKey, out var memberIdRaw)
                || memberIdRaw is not string memberIdStr)
            {
                Debug.LogWarning("[PhotonEventHandler] Remote player left but has no memberId property.");
                return;
            }

            var roomId = new EntityId(PhotonNetwork.CurrentRoom.Name);
            var memberId = new EntityId(memberIdStr);

            var result = _syncHandler.HandleRemotePlayerLeft(roomId, memberId);
            if (result.IsFailure)
            {
                Debug.LogWarning($"[PhotonEventHandler] Failed to remove remote player: {result.Error}");
            }
        }

        // --- Player Properties Update Callbacks ---

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            if (!PhotonNetwork.InRoom) return;

            if (!targetPlayer.CustomProperties.TryGetValue(LobbyPhotonConstants.MemberIdKey, out var midRaw)
                || midRaw is not string midStr)
                return;

            TeamType? team = changedProps.TryGetValue(LobbyPhotonConstants.TeamKey, out var tRaw) && tRaw is int teamInt
                ? (TeamType)teamInt : null;
            bool? isReady = changedProps.TryGetValue(LobbyPhotonConstants.IsReadyKey, out var rRaw) && rRaw is bool readyBool
                ? readyBool : null;

            if (!team.HasValue && !isReady.HasValue) return;

            var roomId = new EntityId(PhotonNetwork.CurrentRoom.Name);
            var memberId = new EntityId(midStr);

            var result = _syncHandler.HandlePlayerPropertiesChanged(roomId, memberId, team, isReady);
            if (result.IsFailure)
                Debug.LogWarning($"[PhotonEventHandler] PropertiesChanged failed: {result.Error}");
        }

        // --- Custom Event Callbacks ---

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code != LobbyPhotonConstants.GameStartedEventCode)
                return;

            if (photonEvent.CustomData is not string roomIdValue || string.IsNullOrWhiteSpace(roomIdValue))
            {
                Debug.LogWarning("[PhotonEventHandler] Received GameStarted event with invalid payload.");
                return;
            }

            var roomId = new EntityId(roomIdValue);
            var result = _syncHandler.HandleGameStarted(roomId);
            if (result.IsFailure)
                Debug.LogWarning($"[PhotonEventHandler] GameStarted event received but could not be applied: {result.Error}");
        }

        // --- Helpers ---

        private static RoomMember BuildMemberFromPlayer(Player player)
        {
            if (!player.CustomProperties.TryGetValue(LobbyPhotonConstants.MemberIdKey, out var midRaw)
                || midRaw is not string midStr)
                return null;

            var memberId = new EntityId(midStr);
            var displayName = player.CustomProperties.TryGetValue(LobbyPhotonConstants.DisplayNameKey, out var dnRaw) && dnRaw is string dnStr
                ? dnStr : player.NickName ?? "Player";
            var team = player.CustomProperties.TryGetValue(LobbyPhotonConstants.TeamKey, out var tRaw) && tRaw is int tInt
                ? (TeamType)tInt : TeamType.None;
            var isReady = player.CustomProperties.TryGetValue(LobbyPhotonConstants.IsReadyKey, out var rRaw) && rRaw is bool rBool && rBool;

            return new RoomMember(memberId, displayName, team, isReady);
        }

        private static List<RoomMember> BuildMembersFromPlayers(PhotonRoom photonRoom)
        {
            var members = new List<RoomMember>();
            foreach (var player in photonRoom.Players.Values)
            {
                var member = BuildMemberFromPlayer(player);
                if (member != null)
                    members.Add(member);
            }
            return members;
        }
    }
}
