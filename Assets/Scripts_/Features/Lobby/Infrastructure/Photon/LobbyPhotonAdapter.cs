using Features.Lobby.Application.Ports;
using Features.Lobby.Application.Events;
using Features.Lobby.Domain;
using ExitGames.Client.Photon;
using Shared.EventBus;
using UnityEngine;
using EntityId = Shared.Kernel.EntityId;
using Result = Shared.Kernel.Result;
using TeamType = Features.Lobby.Domain.TeamType;

namespace Features.Lobby.Infrastructure.Photon
{
    public sealed class LobbyPhotonAdapter : MonoBehaviourPunCallbacks, ILobbyNetworkPort
    {
        private const string MemberIdKey = "memberId";
        private const string TeamKey = "team";
        private const string IsReadyKey = "isReady";
        private const string DisplayNameKey = "displayName";
        private const string RoomDisplayNameKey = "roomDisplayName";
        private const string DefaultGameSceneName = "GameScene";

        private IEventPublisher _eventPublisher;
        private Room _pendingCreateRoom;
        private RoomMember _pendingJoinMember;
        private EntityId _pendingJoinRoomId;
        private EntityId _pendingLeaveRoomId;
        private EntityId _pendingLeaveMemberId;
        private bool _isLeavePending;

        public void Initialize(IEventPublisher eventPublisher)
        {
            _eventPublisher = eventPublisher;
        }

        public Result RequestCreateRoom(Room room)
        {
            if (_eventPublisher == null)
                return Result.Failure("LobbyPhotonAdapter is not initialized.");
            if (room == null)
                return Result.Failure("Room is required.");
            if (!PhotonNetwork.IsConnectedAndReady)
                return Result.Failure("Photon is not connected and ready.");
            if (PhotonNetwork.InRoom)
                return Result.Failure("Already in a room.");
            if (_pendingCreateRoom != null)
                return Result.Failure("CreateRoom is already pending.");
            if (room.Capacity > byte.MaxValue)
                return Result.Failure("Room capacity exceeds Photon max byte size.");

            var owner = room.FindMember(room.OwnerId);
            if (owner == null)
                return Result.Failure("Room owner is required.");

            if (!SetLocalMemberProperties(owner.Id, owner.DisplayName, owner.Team, owner.IsReady))
                return Result.Failure("Local player is unavailable.");

            _pendingCreateRoom = room;

            var options = new RoomOptions
            {
                MaxPlayers = (byte)room.Capacity,
                IsVisible = true,
                IsOpen = true,
                CleanupCacheOnLeave = true,
                CustomRoomProperties = new Hashtable
                {
                    [RoomDisplayNameKey] = room.Name
                },
                CustomRoomPropertiesForLobby = new[] { RoomDisplayNameKey }
            };

            var created = PhotonNetwork.CreateRoom(room.Id.Value, options, TypedLobby.Default);
            if (!created)
            {
                _pendingCreateRoom = null;
                return Result.Failure("Failed to send CreateRoom request to Photon.");
            }

            return Result.Success();
        }

        public Result RequestJoinRoom(EntityId roomId, RoomMember member)
        {
            if (_eventPublisher == null)
                return Result.Failure("LobbyPhotonAdapter is not initialized.");
            if (member == null)
                return Result.Failure("Member is required.");
            if (string.IsNullOrWhiteSpace(roomId.Value))
                return Result.Failure("Room id is required.");
            if (!PhotonNetwork.IsConnectedAndReady)
                return Result.Failure("Photon is not connected and ready.");
            if (PhotonNetwork.InRoom)
                return Result.Failure("Already in a room.");
            if (_pendingJoinMember != null)
                return Result.Failure("JoinRoom is already pending.");

            if (!SetLocalMemberProperties(member.Id, member.DisplayName, member.Team, member.IsReady))
                return Result.Failure("Local player is unavailable.");

            _pendingJoinRoomId = roomId;
            _pendingJoinMember = member;

            var joined = PhotonNetwork.JoinRoom(roomId.Value);
            if (!joined)
            {
                ClearPendingJoin();
                return Result.Failure("Failed to send JoinRoom request to Photon.");
            }

            return Result.Success();
        }

        public Result RequestLeaveRoom(EntityId roomId, EntityId memberId)
        {
            if (_eventPublisher == null)
                return Result.Failure("LobbyPhotonAdapter is not initialized.");
            if (!PhotonNetwork.InRoom)
                return Result.Failure("Cannot leave room outside a room.");
            if (_isLeavePending)
                return Result.Failure("LeaveRoom is already pending.");
            if (!string.Equals(PhotonNetwork.CurrentRoom.Name, roomId.Value, System.StringComparison.Ordinal))
                return Result.Failure("Current room does not match target room id.");

            _pendingLeaveRoomId = roomId;
            _pendingLeaveMemberId = memberId;
            _isLeavePending = true;

            var left = PhotonNetwork.LeaveRoom();
            if (!left)
            {
                ClearPendingLeave();
                return Result.Failure("Failed to send LeaveRoom request to Photon.");
            }

            return Result.Success();
        }

        public Result RequestChangeTeam(EntityId roomId, EntityId memberId, TeamType team)
        {
            if (_eventPublisher == null)
                return Result.Failure("LobbyPhotonAdapter is not initialized.");
            if (!PhotonNetwork.InRoom)
                return Result.Failure("Cannot change team outside a room.");

            if (!TryGetLocalMemberId(out var localMemberId))
                return Result.Failure("Local member id is missing.");

            if (!localMemberId.Equals(memberId))
                return Result.Failure("Can only change local member team.");

            var props = new Hashtable
            {
                [TeamKey] = (int)team
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            return Result.Success();
        }

        public Result RequestSetReady(EntityId roomId, EntityId memberId, bool isReady)
        {
            if (_eventPublisher == null)
                return Result.Failure("LobbyPhotonAdapter is not initialized.");
            if (!PhotonNetwork.InRoom)
                return Result.Failure("Cannot set ready outside a room.");

            if (!TryGetLocalMemberId(out var localMemberId))
                return Result.Failure("Local member id is missing.");

            if (!localMemberId.Equals(memberId))
                return Result.Failure("Can only set local member ready state.");

            var props = new Hashtable
            {
                [IsReadyKey] = isReady
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            return Result.Success();
        }

        public Result RequestStartGame(EntityId roomId)
        {
            if (_eventPublisher == null)
                return Result.Failure("LobbyPhotonAdapter is not initialized.");
            if (!PhotonNetwork.InRoom)
                return Result.Failure("Cannot start game outside a room.");
            if (!PhotonNetwork.IsMasterClient)
                return Result.Failure("Only the room master can start the game.");

            PhotonNetwork.LoadLevel(DefaultGameSceneName);
            return Result.Success();
        }

        public override void OnCreatedRoom()
        {
            if (_pendingCreateRoom == null)
                return;

            Publish(new RoomCreatedEvent(_pendingCreateRoom));
            _pendingCreateRoom = null;
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            _pendingCreateRoom = null;
            Publish(new LobbyErrorEvent($"Create room failed ({returnCode}): {message}"));
        }

        public override void OnJoinedRoom()
        {
            if (_pendingJoinMember == null)
                return;

            Publish(new RoomJoinedEvent(_pendingJoinRoomId, _pendingJoinMember));
            ClearPendingJoin();
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            ClearPendingJoin();
            Publish(new LobbyErrorEvent($"Join room failed ({returnCode}): {message}"));
        }

        public override void OnLeftRoom()
        {
            if (!_isLeavePending)
                return;

            Publish(new RoomLeftEvent(_pendingLeaveRoomId, _pendingLeaveMemberId));
            ClearPendingLeave();
        }

        private static bool SetLocalMemberProperties(EntityId memberId, string displayName, TeamType team, bool isReady)
        {
            if (PhotonNetwork.LocalPlayer == null)
                return false;

            var props = new Hashtable
            {
                [MemberIdKey] = memberId.Value,
                [DisplayNameKey] = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim(),
                [TeamKey] = (int)team,
                [IsReadyKey] = isReady
            };

            PhotonNetwork.LocalPlayer.NickName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim();
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            return true;
        }

        private static bool TryGetLocalMemberId(out EntityId memberId)
        {
            memberId = default;

            if (PhotonNetwork.LocalPlayer == null)
                return false;
            if (!PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(MemberIdKey, out var value))
                return false;

            var raw = value as string;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            memberId = new EntityId(raw);
            return true;
        }

        private void Publish<T>(T e)
        {
            if (_eventPublisher == null)
            {
                Debug.LogError("[LobbyPhotonAdapter] EventPublisher is not initialized.");
                return;
            }

            _eventPublisher.Publish(e);
        }

        private void ClearPendingJoin()
        {
            _pendingJoinMember = null;
            _pendingJoinRoomId = default;
        }

        private void ClearPendingLeave()
        {
            _pendingLeaveRoomId = default;
            _pendingLeaveMemberId = default;
            _isLeavePending = false;
        }
    }
}
