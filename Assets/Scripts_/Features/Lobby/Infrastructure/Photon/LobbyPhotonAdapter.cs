using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Features.Lobby.Application.Ports;
using Features.Lobby.Domain;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using DomainRoom = Features.Lobby.Domain.Room;
using PhotonPlayer = Photon.Realtime.Player;
using PhotonRoom = Photon.Realtime.Room;
using Result = Shared.Kernel.Result;
using Shared.Kernel;

namespace Features.Lobby.Infrastructure.Photon
{
    public sealed class LobbyPhotonAdapter
        : MonoBehaviourPunCallbacks,
            ILobbyNetworkCommandPort,
            IOnEventCallback,
            ILobbyNetworkCallbackPort
    {
        [SerializeField]
        private string DefaultGameSceneName = "JG_GameScene";

        private readonly PhotonPlayerPropertyManager _propertyManager = new();

        // Pending state
        private DomainRoom _pendingCreateRoom;
        private bool _pendingJoin;
        private DomainEntityId _pendingLeaveRoomId;
        private DomainEntityId _pendingLeaveMemberId;

        // ILobbyNetworkCallbackPort callbacks
        public Action<DomainRoom> OnCreateRoomSucceeded { get; set; }
        public Action<string> OnErrorOccurred { get; set; }
        public Action<JoinRoomData> OnJoinRoomSucceeded { get; set; }
        public Action<DomainEntityId, DomainEntityId> OnLeaveRoomSucceeded { get; set; }
        public Action<DomainEntityId, RoomMember> OnRemotePlayerEntered { get; set; }
        public Action<DomainEntityId, DomainEntityId> OnRemotePlayerLeft { get; set; }
        public Action<PlayerPropertiesData> OnPlayerPropertiesChanged { get; set; }
        public Action<DomainEntityId> OnGameStarted { get; set; }
        public Action<List<RoomListItem>> OnRoomListUpdated { get; set; }

        // ===== ILobbyNetworkCommandPort =====

        public Result CreateRoom(DomainRoom room)
        {
            if (room == null)
                return Result.Failure("Room is required.");

            var connected = ValidateConnected();
            if (!connected.IsSuccess)
                return connected;

            var notInRoom = ValidateNotInRoom();
            if (!notInRoom.IsSuccess)
                return notInRoom;

            if (room.Capacity > byte.MaxValue)
                return Result.Failure("Room capacity exceeds Photon max byte size.");

            var owner = room.FindMember(room.OwnerId);
            if (owner == null)
                return Result.Failure("Room owner is required.");

            if (!_propertyManager.SetLocalMemberProperties(owner))
                return Result.Failure("Local player is unavailable.");

            var options = new RoomOptions
            {
                MaxPlayers = (byte)room.Capacity,
                IsVisible = true,
                IsOpen = true,
                CleanupCacheOnLeave = true,
                CustomRoomProperties = new Hashtable
                {
                    [LobbyPhotonConstants.RoomDisplayNameKey] = room.Name,
                },
                CustomRoomPropertiesForLobby = new[] { LobbyPhotonConstants.RoomDisplayNameKey },
            };

            _pendingCreateRoom = room;
            _pendingJoin = false;

            var created = PhotonNetwork.CreateRoom(room.Id.Value, options, TypedLobby.Default);
            if (!created)
            {
                ClearPending();
                return Result.Failure("Failed to send CreateRoom request to Photon.");
            }

            return Result.Success();
        }

        public Result JoinRoom(DomainEntityId roomId, RoomMember localMember)
        {
            if (localMember == null)
                return Result.Failure("Member is required.");

            if (string.IsNullOrWhiteSpace(roomId.Value))
                return Result.Failure("Room id is required.");

            var connected = ValidateConnected();
            if (!connected.IsSuccess)
                return connected;

            var notInRoom = ValidateNotInRoom();
            if (!notInRoom.IsSuccess)
                return notInRoom;

            if (!_propertyManager.SetLocalMemberProperties(localMember))
                return Result.Failure("Local player is unavailable.");

            _pendingCreateRoom = null;
            _pendingJoin = true;

            var joined = PhotonNetwork.JoinRoom(roomId.Value);
            if (!joined)
            {
                ClearPending();
                return Result.Failure("Failed to send JoinRoom request to Photon.");
            }

            return Result.Success();
        }

        public Result LeaveRoom(DomainEntityId roomId, DomainEntityId memberId)
        {
            var inRoom = ValidateInRoom();
            if (!inRoom.IsSuccess)
                return inRoom;

            if (
                !string.Equals(
                    PhotonNetwork.CurrentRoom.Name,
                    roomId.Value,
                    StringComparison.Ordinal
                )
            )
                return Result.Failure("Current room does not match target room id.");

            _pendingLeaveRoomId = roomId;
            _pendingLeaveMemberId = memberId;

            var left = PhotonNetwork.LeaveRoom();
            if (!left)
            {
                ClearPending();
                return Result.Failure("Failed to send LeaveRoom request to Photon.");
            }

            return Result.Success();
        }

        public Result ChangeTeam(DomainEntityId memberId, TeamType team)
        {
            var inRoom = ValidateInRoom();
            if (!inRoom.IsSuccess)
                return inRoom;

            var localMember = ValidateLocalMember(memberId);
            if (!localMember.IsSuccess)
                return localMember;

            PhotonNetwork.LocalPlayer.SetCustomProperties(
                new Hashtable { [LobbyPhotonConstants.TeamKey] = (int)team }
            );
            return Result.Success();
        }

        public Result SetReady(DomainEntityId memberId, bool isReady)
        {
            var inRoom = ValidateInRoom();
            if (!inRoom.IsSuccess)
                return inRoom;

            var localMember = ValidateLocalMember(memberId);
            if (!localMember.IsSuccess)
                return localMember;

            PhotonNetwork.LocalPlayer.SetCustomProperties(
                new Hashtable { [LobbyPhotonConstants.IsReadyKey] = isReady }
            );
            return Result.Success();
        }

        public Result StartGame(DomainEntityId roomId)
        {
            var inRoom = ValidateInRoom();
            if (!inRoom.IsSuccess)
                return inRoom;

            if (!PhotonNetwork.IsMasterClient)
                return Result.Failure("Only the room master can start the game.");

            var raised = PhotonNetwork.RaiseEvent(
                LobbyPhotonConstants.GameStartedEventCode,
                roomId.Value,
                new RaiseEventOptions { Receivers = ReceiverGroup.All },
                SendOptions.SendReliable
            );
            if (!raised)
                return Result.Failure("Failed to raise game started event.");

            PhotonNetwork.LoadLevel(DefaultGameSceneName);
            return Result.Success();
        }

        // ===== Photon Callbacks =====

        public override void OnCreatedRoom()
        {
            var room = _pendingCreateRoom;
            _pendingCreateRoom = null;

            if (room == null)
            {
                Debug.LogWarning(
                    "[LobbyPhotonAdapter] Unexpected OnCreatedRoom: no pending create."
                );
                return;
            }

            OnCreateRoomSucceeded?.Invoke(room);
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            _pendingCreateRoom = null;
            OnErrorOccurred?.Invoke($"Create room failed ({returnCode}): {message}");
        }

        public override void OnJoinedRoom()
        {
            // Creator also receives OnJoinedRoom after OnCreatedRoom — skip it
            if (!_pendingJoin)
                return;
            _pendingJoin = false;

            var photonRoom = PhotonNetwork.CurrentRoom;
            var roomId = new DomainEntityId(photonRoom.Name);
            var roomName =
                photonRoom.CustomProperties.TryGetValue(
                    LobbyPhotonConstants.RoomDisplayNameKey,
                    out var nameRaw
                ) && nameRaw is string nameStr
                    ? nameStr
                    : photonRoom.Name;

            var members = BuildMembersFromPlayers(photonRoom);
            if (members.Count == 0)
            {
                Debug.LogError(
                    "[LobbyPhotonAdapter] OnJoinedRoom: no members could be built from players."
                );
                return;
            }

            DomainEntityId masterMemberId = default;
            if (photonRoom.Players.TryGetValue(photonRoom.MasterClientId, out var masterPlayer))
            {
                var m = BuildMemberFromPlayer(masterPlayer);
                if (m != null)
                    masterMemberId = m.Id;
            }

            var localMember = BuildMemberFromPlayer(PhotonNetwork.LocalPlayer);
            var localMemberId = localMember != null ? localMember.Id : default;

            OnJoinRoomSucceeded?.Invoke(
                new JoinRoomData(
                    roomId,
                    roomName,
                    photonRoom.MaxPlayers,
                    members,
                    masterMemberId,
                    localMemberId
                )
            );
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            _pendingJoin = false;
            OnErrorOccurred?.Invoke($"Join room failed ({returnCode}): {message}");
        }

        public override void OnLeftRoom()
        {
            var roomId = _pendingLeaveRoomId;
            var memberId = _pendingLeaveMemberId;
            _pendingLeaveRoomId = default;
            _pendingLeaveMemberId = default;

            if (string.IsNullOrWhiteSpace(roomId.Value))
            {
                Debug.LogWarning(
                    "[LobbyPhotonAdapter] Unexpected OnLeftRoom: no pending leave info."
                );
                return;
            }

            OnLeaveRoomSucceeded?.Invoke(roomId, memberId);
        }

        public override void OnPlayerEnteredRoom(PhotonPlayer newPlayer)
        {
            if (!PhotonNetwork.InRoom)
                return;
            if (newPlayer == PhotonNetwork.LocalPlayer)
                return;

            var member = BuildMemberFromPlayer(newPlayer);
            if (member == null)
            {
                Debug.LogWarning(
                    "[LobbyPhotonAdapter] Remote player entered but has no memberId property."
                );
                return;
            }

            var roomId = new DomainEntityId(PhotonNetwork.CurrentRoom.Name);
            OnRemotePlayerEntered?.Invoke(roomId, member);
        }

        public override void OnPlayerLeftRoom(PhotonPlayer otherPlayer)
        {
            if (!PhotonNetwork.InRoom)
                return;
            if (otherPlayer == PhotonNetwork.LocalPlayer)
                return;

            if (
                !otherPlayer.CustomProperties.TryGetValue(
                    LobbyPhotonConstants.MemberIdKey,
                    out var memberIdRaw
                ) || memberIdRaw is not string memberIdStr
            )
            {
                Debug.LogWarning(
                    "[LobbyPhotonAdapter] Remote player left but has no memberId property."
                );
                return;
            }

            var roomId = new DomainEntityId(PhotonNetwork.CurrentRoom.Name);
            var memberId = new DomainEntityId(memberIdStr);

            OnRemotePlayerLeft?.Invoke(roomId, memberId);
        }

        public override void OnPlayerPropertiesUpdate(PhotonPlayer targetPlayer, Hashtable changedProps)
        {
            if (!PhotonNetwork.InRoom)
                return;

            if (
                !targetPlayer.CustomProperties.TryGetValue(
                    LobbyPhotonConstants.MemberIdKey,
                    out var midRaw
                ) || midRaw is not string midStr
            )
                return;

            TeamType? team =
                changedProps.TryGetValue(LobbyPhotonConstants.TeamKey, out var tRaw)
                && tRaw is int teamInt
                    ? (TeamType)teamInt
                    : null;
            bool? isReady =
                changedProps.TryGetValue(LobbyPhotonConstants.IsReadyKey, out var rRaw)
                && rRaw is bool readyBool
                    ? readyBool
                    : null;

            if (!team.HasValue && !isReady.HasValue)
                return;

            var roomId = new DomainEntityId(PhotonNetwork.CurrentRoom.Name);
            var memberId = new DomainEntityId(midStr);

            OnPlayerPropertiesChanged?.Invoke(
                new PlayerPropertiesData(roomId, memberId, team, isReady)
            );
        }

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code != LobbyPhotonConstants.GameStartedEventCode)
                return;

            if (
                photonEvent.CustomData is not string roomIdValue
                || string.IsNullOrWhiteSpace(roomIdValue)
            )
            {
                Debug.LogWarning(
                    "[LobbyPhotonAdapter] Received GameStarted event with invalid payload."
                );
                return;
            }

            var roomId = new DomainEntityId(roomIdValue);
            OnGameStarted?.Invoke(roomId);
        }

        public override void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            var items = new List<RoomListItem>();
            foreach (var info in roomList)
            {
                if (info.RemovedFromList || !info.IsVisible)
                    continue;

                var roomId = new DomainEntityId(info.Name);
                string displayName = info.Name;
                if (info.CustomProperties.TryGetValue(
                        LobbyPhotonConstants.RoomDisplayNameKey, out var nameRaw)
                    && nameRaw is string nameStr)
                {
                    displayName = nameStr;
                }

                items.Add(new RoomListItem(roomId, displayName, info.PlayerCount, info.MaxPlayers, info.IsOpen));
            }

            OnRoomListUpdated?.Invoke(items);
        }

        // ===== Helpers =====

        private void ClearPending()
        {
            _pendingCreateRoom = null;
            _pendingJoin = false;
            _pendingLeaveRoomId = default;
            _pendingLeaveMemberId = default;
        }

        private static RoomMember BuildMemberFromPlayer(PhotonPlayer player)
        {
            if (
                !player.CustomProperties.TryGetValue(
                    LobbyPhotonConstants.MemberIdKey,
                    out var midRaw
                ) || midRaw is not string midStr
            )
                return null;

            var memberId = new DomainEntityId(midStr);
            var displayName =
                player.CustomProperties.TryGetValue(
                    LobbyPhotonConstants.DisplayNameKey,
                    out var dnRaw
                ) && dnRaw is string dnStr
                    ? dnStr
                    : player.NickName ?? "Player";
            var team =
                player.CustomProperties.TryGetValue(LobbyPhotonConstants.TeamKey, out var tRaw)
                && tRaw is int tInt
                    ? (TeamType)tInt
                    : TeamType.None;
            var isReady =
                player.CustomProperties.TryGetValue(LobbyPhotonConstants.IsReadyKey, out var rRaw)
                && rRaw is bool rBool
                && rBool;

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

        private static Result ValidateConnected() =>
            PhotonNetwork.IsConnectedAndReady
                ? Result.Success()
                : Result.Failure("Photon is not connected and ready.");

        private static Result ValidateNotInRoom() =>
            !PhotonNetwork.InRoom ? Result.Success() : Result.Failure("Already in a room.");

        private static Result ValidateInRoom() =>
            PhotonNetwork.InRoom ? Result.Success() : Result.Failure("Not in a room.");

        private Result ValidateLocalMember(DomainEntityId memberId)
        {
            if (!_propertyManager.TryGetLocalMemberId(out var localMemberId))
                return Result.Failure("Local member id is missing.");

            return localMemberId.Equals(memberId)
                ? Result.Success()
                : Result.Failure("Can only modify local member.");
        }
    }
}
