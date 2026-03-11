using System;
using Photon.Pun;
using Photon.Realtime;
using Features.Lobby.Application.Ports;
using ExitGames.Client.Photon;
using Shared.EventBus;
using UnityEngine;
using EntityId = Shared.Kernel.EntityId;
using Result = Shared.Kernel.Result;
using Room = Features.Lobby.Domain.Room;
using RoomMember = Features.Lobby.Domain.RoomMember;
using TeamType = Features.Lobby.Domain.TeamType;

namespace Features.Lobby.Infrastructure.Photon
{
    public sealed class LobbyPhotonAdapter : MonoBehaviour, ILobbyNetworkPort
    {
        private const string DefaultGameSceneName = "GameScene";

        private readonly PhotonPlayerPropertyManager _propertyManager = new();

        private PhotonNetworkEventHandler _eventHandler;

        public void Initialize(ILobbyRepository repository, IEventPublisher publisher)
        {
            _eventHandler = GetComponent<PhotonNetworkEventHandler>();
            if (_eventHandler == null)
                _eventHandler = gameObject.AddComponent<PhotonNetworkEventHandler>();

            _eventHandler.Initialize(repository, publisher, _propertyManager);
        }

        public Result CreateRoom(Room room)
        {
            if (room == null)
                return Result.Failure("Room is required.");

            var connected = ValidateConnected();
            if (!connected.IsSuccess) return connected;

            var notInRoom = ValidateNotInRoom();
            if (!notInRoom.IsSuccess) return notInRoom;

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
                CustomRoomProperties = new Hashtable { [LobbyPhotonConstants.RoomDisplayNameKey] = room.Name },
                CustomRoomPropertiesForLobby = new[] { LobbyPhotonConstants.RoomDisplayNameKey }
            };

            _eventHandler.SetPendingCreate(room);

            var created = PhotonNetwork.CreateRoom(room.Id.Value, options, TypedLobby.Default);
            if (!created)
            {
                _eventHandler.ClearPending();
                return Result.Failure("Failed to send CreateRoom request to Photon.");
            }

            return Result.Success();
        }

        public Result JoinRoom(EntityId roomId, RoomMember localMember)
        {
            if (localMember == null)
                return Result.Failure("Member is required.");

            if (string.IsNullOrWhiteSpace(roomId.Value))
                return Result.Failure("Room id is required.");

            var connected = ValidateConnected();
            if (!connected.IsSuccess) return connected;

            var notInRoom = ValidateNotInRoom();
            if (!notInRoom.IsSuccess) return notInRoom;

            if (!_propertyManager.SetLocalMemberProperties(localMember))
                return Result.Failure("Local player is unavailable.");

            _eventHandler.SetPendingJoin();

            var joined = PhotonNetwork.JoinRoom(roomId.Value);
            if (!joined)
            {
                _eventHandler.ClearPending();
                return Result.Failure("Failed to send JoinRoom request to Photon.");
            }

            return Result.Success();
        }

        public Result LeaveRoom(EntityId roomId, EntityId memberId)
        {
            var inRoom = ValidateInRoom();
            if (!inRoom.IsSuccess) return inRoom;

            if (!string.Equals(PhotonNetwork.CurrentRoom.Name, roomId.Value, StringComparison.Ordinal))
                return Result.Failure("Current room does not match target room id.");

            _eventHandler.SetPendingLeave(roomId, memberId);

            var left = PhotonNetwork.LeaveRoom();
            if (!left)
            {
                _eventHandler.ClearPending();
                return Result.Failure("Failed to send LeaveRoom request to Photon.");
            }

            return Result.Success();
        }

        public Result ChangeTeam(EntityId memberId, TeamType team)
        {
            var inRoom = ValidateInRoom();
            if (!inRoom.IsSuccess) return inRoom;

            var localMember = ValidateLocalMember(memberId);
            if (!localMember.IsSuccess) return localMember;

            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [LobbyPhotonConstants.TeamKey] = (int)team });
            return Result.Success();
        }

        public Result SetReady(EntityId memberId, bool isReady)
        {
            var inRoom = ValidateInRoom();
            if (!inRoom.IsSuccess) return inRoom;

            var localMember = ValidateLocalMember(memberId);
            if (!localMember.IsSuccess) return localMember;

            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [LobbyPhotonConstants.IsReadyKey] = isReady });
            return Result.Success();
        }

        public Result StartGame(EntityId roomId)
        {
            var inRoom = ValidateInRoom();
            if (!inRoom.IsSuccess) return inRoom;

            if (!PhotonNetwork.IsMasterClient)
                return Result.Failure("Only the room master can start the game.");

            var raised = PhotonNetwork.RaiseEvent(
                LobbyPhotonConstants.GameStartedEventCode,
                roomId.Value,
                new RaiseEventOptions { Receivers = ReceiverGroup.All },
                SendOptions.SendReliable);
            if (!raised)
                return Result.Failure("Failed to raise game started event.");

            PhotonNetwork.LoadLevel(DefaultGameSceneName);
            return Result.Success();
        }

        // --- Validation Helpers ---

        private static Result ValidateConnected() =>
            PhotonNetwork.IsConnectedAndReady ? Result.Success() : Result.Failure("Photon is not connected and ready.");

        private static Result ValidateNotInRoom() =>
            !PhotonNetwork.InRoom ? Result.Success() : Result.Failure("Already in a room.");

        private static Result ValidateInRoom() =>
            PhotonNetwork.InRoom ? Result.Success() : Result.Failure("Not in a room.");

        private Result ValidateLocalMember(EntityId memberId)
        {
            if (!_propertyManager.TryGetLocalMemberId(out var localMemberId))
                return Result.Failure("Local member id is missing.");

            return localMemberId.Equals(memberId)
                ? Result.Success()
                : Result.Failure("Can only modify local member.");
        }
    }
}
