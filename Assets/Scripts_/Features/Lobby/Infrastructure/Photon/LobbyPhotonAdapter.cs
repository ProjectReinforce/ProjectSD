using System;
using Shared.Network;
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
using Op = Features.Lobby.Infrastructure.Photon.LobbyPhotonOperation;

namespace Features.Lobby.Infrastructure.Photon
{
    /// <summary>
    /// Port implementation for Photon network communication.
    /// Single responsibility: implementing ILobbyNetworkPort contract.
    /// </summary>
    public sealed class LobbyPhotonAdapter : MonoBehaviour, ILobbyNetworkPort
    {
        private const string RoomDisplayNameKey = "roomDisplayName";
        private const string DefaultGameSceneName = "GameScene";
        private const byte GameStartedEventCode = 100;

        private readonly PendingCallbackTracker<LobbyPhotonOperation> _requestManager = new();
        private readonly PhotonPlayerPropertyManager _propertyManager = new();

        private PhotonNetworkEventHandler _eventHandler;

        public void Initialize(ILobbyRepository repository, IEventPublisher publisher)
        {
            _eventHandler = GetComponent<PhotonNetworkEventHandler>();
            if (_eventHandler == null)
                _eventHandler = gameObject.AddComponent<PhotonNetworkEventHandler>();

            _eventHandler.Initialize(_requestManager, repository, publisher);
        }

        public Result RequestCreateRoom(Room room, Action onSuccess, Action<string> onFailure)
        {
            if (room == null)
                return Result.Failure("Room is required.");

            var connected = ValidateConnected();
            if (!connected.IsSuccess) return connected;

            var notInRoom = ValidateNotInRoom();
            if (!notInRoom.IsSuccess) return notInRoom;

            if (_requestManager.IsPending(Op.Create))
                return Result.Failure("CreateRoom is already pending.");

            if (room.Capacity > byte.MaxValue)
                return Result.Failure("Room capacity exceeds Photon max byte size.");

            var owner = room.FindMember(room.OwnerId);
            if (owner == null)
                return Result.Failure("Room owner is required.");

            if (!_propertyManager.SetLocalMemberProperties(owner))
                return Result.Failure("Local player is unavailable.");

            _requestManager.Set(Op.Create, onSuccess, onFailure);

            var options = new RoomOptions
            {
                MaxPlayers = (byte)room.Capacity,
                IsVisible = true,
                IsOpen = true,
                CleanupCacheOnLeave = true,
                CustomRoomProperties = new Hashtable { [RoomDisplayNameKey] = room.Name },
                CustomRoomPropertiesForLobby = new[] { RoomDisplayNameKey }
            };

            var created = PhotonNetwork.CreateRoom(room.Id.Value, options, TypedLobby.Default);
            if (!created)
            {
                _requestManager.Clear(Op.Create);
                return Result.Failure("Failed to send CreateRoom request to Photon.");
            }

            return Result.Success();
        }

        public Result RequestJoinRoom(EntityId roomId, RoomMember member, Action onSuccess, Action<string> onFailure)
        {
            if (member == null)
                return Result.Failure("Member is required.");

            if (string.IsNullOrWhiteSpace(roomId.Value))
                return Result.Failure("Room id is required.");

            var connected = ValidateConnected();
            if (!connected.IsSuccess) return connected;

            var notInRoom = ValidateNotInRoom();
            if (!notInRoom.IsSuccess) return notInRoom;

            if (_requestManager.IsPending(Op.Join))
                return Result.Failure("JoinRoom is already pending.");

            if (!_propertyManager.SetLocalMemberProperties(member))
                return Result.Failure("Local player is unavailable.");

            _requestManager.Set(Op.Join, onSuccess, onFailure);

            var joined = PhotonNetwork.JoinRoom(roomId.Value);
            if (!joined)
            {
                _requestManager.Clear(Op.Join);
                return Result.Failure("Failed to send JoinRoom request to Photon.");
            }

            return Result.Success();
        }

        public Result RequestLeaveRoom(EntityId roomId, EntityId memberId, Action onSuccess, Action<string> onFailure)
        {
            var inRoom = ValidateInRoom();
            if (!inRoom.IsSuccess) return inRoom;

            if (_requestManager.IsPending(Op.Leave))
                return Result.Failure("LeaveRoom is already pending.");

            if (!string.Equals(PhotonNetwork.CurrentRoom.Name, roomId.Value, StringComparison.Ordinal))
                return Result.Failure("Current room does not match target room id.");

            _requestManager.Set(Op.Leave, onSuccess, onFailure);

            var left = PhotonNetwork.LeaveRoom();
            if (!left)
            {
                _requestManager.Clear(Op.Leave);
                return Result.Failure("Failed to send LeaveRoom request to Photon.");
            }

            return Result.Success();
        }

        public Result RequestChangeTeam(EntityId roomId, EntityId memberId, TeamType team, Action onSuccess, Action<string> onFailure)
        {
            var inRoom = ValidateInRoom();
            if (!inRoom.IsSuccess) return inRoom;

            if (_requestManager.IsPending(Op.ChangeTeam))
                return Result.Failure("ChangeTeam is already pending.");

            var localMember = ValidateLocalMember(memberId);
            if (!localMember.IsSuccess) return localMember;

            _requestManager.Set(Op.ChangeTeam, onSuccess, onFailure);

            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { ["team"] = (int)team });
            return Result.Success();
        }

        public Result RequestSetReady(EntityId roomId, EntityId memberId, bool isReady, Action onSuccess, Action<string> onFailure)
        {
            var inRoom = ValidateInRoom();
            if (!inRoom.IsSuccess) return inRoom;

            if (_requestManager.IsPending(Op.SetReady))
                return Result.Failure("SetReady is already pending.");

            var localMember = ValidateLocalMember(memberId);
            if (!localMember.IsSuccess) return localMember;

            _requestManager.Set(Op.SetReady, onSuccess, onFailure);

            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { ["isReady"] = isReady });
            return Result.Success();
        }

        public Result RequestStartGame(EntityId roomId)
        {
            var inRoom = ValidateInRoom();
            if (!inRoom.IsSuccess) return inRoom;

            if (!PhotonNetwork.IsMasterClient)
                return Result.Failure("Only the room master can start the game.");

            var raised = PhotonNetwork.RaiseEvent(
                GameStartedEventCode,
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

    internal enum LobbyPhotonOperation
    {
        Create,
        Join,
        Leave,
        ChangeTeam,
        SetReady
    }
}
