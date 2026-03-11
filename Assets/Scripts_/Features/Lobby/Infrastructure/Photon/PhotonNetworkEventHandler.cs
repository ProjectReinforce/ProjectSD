using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using Features.Lobby.Application.Events;
using Features.Lobby.Application.Ports;
using Features.Lobby.Domain;
using Shared.EventBus;
using UnityEngine;
using EntityId = Shared.Kernel.EntityId;

using DomainRoom = Features.Lobby.Domain.Room;
using PhotonRoom = Photon.Realtime.Room;

namespace Features.Lobby.Infrastructure.Photon
{
    public sealed class PhotonNetworkEventHandler : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        private ILobbyRepository _repository;
        private IEventPublisher _publisher;
        private PhotonPlayerPropertyManager _propertyManager;

        // Pending state (data only, no callbacks)
        private DomainRoom _pendingCreateRoom;
        private bool _pendingJoin;
        private EntityId _pendingLeaveRoomId;
        private EntityId _pendingLeaveMemberId;

        internal void Initialize(ILobbyRepository repository, IEventPublisher publisher, PhotonPlayerPropertyManager propertyManager)
        {
            _repository = repository;
            _publisher = publisher;
            _propertyManager = propertyManager;
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

            var lobby = _repository.LoadLobby();
            var addResult = lobby.AddRoom(room);
            if (addResult.IsFailure)
            {
                Debug.LogError($"[PhotonEventHandler] OnCreatedRoom: {addResult.Error}");
                return;
            }

            _repository.SaveLobby(lobby);
            _publisher.Publish(new LobbyUpdatedEvent(lobby));
            _publisher.Publish(new RoomUpdatedEvent(room));
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            _pendingCreateRoom = null;
            _publisher.Publish(new LobbyErrorEvent($"Create room failed ({returnCode}): {message}"));
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

            RoomMember ownerMember = null;
            if (photonRoom.Players.TryGetValue(photonRoom.MasterClientId, out var masterPlayer))
            {
                var m = BuildMemberFromPlayer(masterPlayer);
                if (m != null)
                    ownerMember = members.Find(x => x.Id.Equals(m.Id));
            }
            ownerMember ??= members[0];

            var roomResult = DomainRoom.Create(roomId, roomName, photonRoom.MaxPlayers, ownerMember);
            if (roomResult.IsFailure)
            {
                Debug.LogError($"[PhotonEventHandler] OnJoinedRoom: {roomResult.Error}");
                return;
            }

            var room = roomResult.Value;
            foreach (var member in members)
            {
                if (member.Id.Equals(ownerMember.Id)) continue;
                room.AddMember(member);
            }

            var lobby = _repository.LoadLobby();
            var addResult = lobby.AddRoom(room);
            if (addResult.IsFailure)
            {
                Debug.LogError($"[PhotonEventHandler] OnJoinedRoom: {addResult.Error}");
                return;
            }

            _repository.SaveLobby(lobby);
            _publisher.Publish(new LobbyUpdatedEvent(lobby));
            _publisher.Publish(new RoomUpdatedEvent(room));
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            _pendingJoin = false;
            _publisher.Publish(new LobbyErrorEvent($"Join room failed ({returnCode}): {message}"));
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

            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null) return;

            room.RemoveMember(memberId);

            if (room.Members.Count == 0)
                lobby.RemoveRoom(roomId);

            _repository.SaveLobby(lobby);
            _publisher.Publish(new LobbyUpdatedEvent(lobby));

            if (room.Members.Count > 0)
                _publisher.Publish(new RoomUpdatedEvent(room));
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
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
            {
                Debug.LogWarning("[PhotonEventHandler] Remote player entered but room not found in domain.");
                return;
            }

            var addResult = room.AddMember(member);
            if (addResult.IsFailure)
            {
                Debug.LogWarning($"[PhotonEventHandler] Failed to add remote player: {addResult.Error}");
                return;
            }

            _repository.SaveLobby(lobby);
            _publisher.Publish(new RoomUpdatedEvent(room));
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

            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null) return;

            var removeResult = room.RemoveMember(memberId);
            if (removeResult.IsFailure)
            {
                Debug.LogWarning($"[PhotonEventHandler] Failed to remove remote player: {removeResult.Error}");
                return;
            }

            _repository.SaveLobby(lobby);
            _publisher.Publish(new RoomUpdatedEvent(room));
        }

        // --- Player Properties Update Callbacks ---

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            if (!PhotonNetwork.InRoom) return;

            var roomId = new EntityId(PhotonNetwork.CurrentRoom.Name);
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null) return;

            if (!targetPlayer.CustomProperties.TryGetValue(LobbyPhotonConstants.MemberIdKey, out var midRaw)
                || midRaw is not string midStr)
                return;

            var memberId = new EntityId(midStr);
            var changed = false;

            if (changedProps.ContainsKey(LobbyPhotonConstants.TeamKey) && changedProps[LobbyPhotonConstants.TeamKey] is int teamInt)
            {
                var result = room.ChangeTeam(memberId, (TeamType)teamInt);
                if (result.IsSuccess) changed = true;
                else Debug.LogWarning($"[PhotonEventHandler] ChangeTeam failed: {result.Error}");
            }

            if (changedProps.ContainsKey(LobbyPhotonConstants.IsReadyKey) && changedProps[LobbyPhotonConstants.IsReadyKey] is bool isReady)
            {
                var result = room.SetReady(memberId, isReady);
                if (result.IsSuccess) changed = true;
                else Debug.LogWarning($"[PhotonEventHandler] SetReady failed: {result.Error}");
            }

            if (changed)
            {
                _repository.SaveLobby(lobby);
                _publisher.Publish(new RoomUpdatedEvent(room));
            }
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
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
            {
                Debug.LogWarning("[PhotonEventHandler] GameStarted event received but room not found.");
                return;
            }

            _publisher.Publish(new GameStartedEvent(room));
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
