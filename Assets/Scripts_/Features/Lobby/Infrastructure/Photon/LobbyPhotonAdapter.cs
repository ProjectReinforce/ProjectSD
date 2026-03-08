using Photon.Pun;
using Photon.Realtime;
using Features.Lobby.Application;
using Features.Lobby.Application.Ports;
using ExitGames.Client.Photon;

using UnityEngine;
using EntityId = Shared.Kernel.EntityId;
using Result = Shared.Kernel.Result;
using Room = Features.Lobby.Domain.Room;
using RoomMember = Features.Lobby.Domain.RoomMember;
using TeamType = Features.Lobby.Domain.TeamType;

namespace Features.Lobby.Infrastructure.Photon
{
    /// <summary>
    /// Port implementation for Photon network communication.
    /// Single responsibility: implementing ILobbyNetworkPort contract.
    /// Delegates to specialized managers:
    /// - PhotonRequestManager: pending state tracking
    /// - PhotonPlayerPropertyManager: player properties
    /// - PhotonNetworkEventHandler: callback handling
    /// </summary>
    public sealed class LobbyPhotonAdapter : MonoBehaviour, ILobbyNetworkPort
    {
        private const string RoomDisplayNameKey = "roomDisplayName";
        private const string DefaultGameSceneName = "GameScene";
        private const byte GameStartedEventCode = 100;

        private readonly PhotonRequestManager _requestManager = new();
        private readonly PhotonPlayerPropertyManager _propertyManager = new();
        
        private PhotonNetworkEventHandler _eventHandler;
        private LobbyConfirmHandler _callback;

        /// <summary>
        /// Called by Bootstrap to wire up the callback handler and event handler.
        /// </summary>
        public void Initialize(LobbyConfirmHandler callback)
        {
            _callback = callback;
            
            // Find or create event handler (separate GameObject or same)
            _eventHandler = GetComponent<PhotonNetworkEventHandler>();
            if (_eventHandler == null)
            {
                _eventHandler = gameObject.AddComponent<PhotonNetworkEventHandler>();
            }
            
            _eventHandler.Initialize(callback, _requestManager);
        }

        public Result RequestCreateRoom(Room room)
        {
            if (room == null)
                return Result.Failure("Room is required.");
            
            var connected = ValidateConnected();
            if (!connected.IsSuccess)
                return connected;
            
            var notInRoom = ValidateNotInRoom();
            if (!notInRoom.IsSuccess)
                return notInRoom;
            
            if (_requestManager.HasPendingCreate)
                return Result.Failure("CreateRoom is already pending.");
            
            if (room.Capacity > byte.MaxValue)
                return Result.Failure("Room capacity exceeds Photon max byte size.");

            var owner = room.FindMember(room.OwnerId);
            if (owner == null)
                return Result.Failure("Room owner is required.");

            if (!_propertyManager.SetLocalMemberProperties(owner.Id, owner.DisplayName, owner.Team, owner.IsReady))
                return Result.Failure("Local player is unavailable.");

            _requestManager.SetPendingCreate(room);

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
                _requestManager.ClearPendingCreate();
                return Result.Failure("Failed to send CreateRoom request to Photon.");
            }

            return Result.Success();
        }

        public Result RequestJoinRoom(EntityId roomId, RoomMember member)
        {
            if (member == null)
                return Result.Failure("Member is required.");
            
            if (string.IsNullOrWhiteSpace(roomId.Value))
                return Result.Failure("Room id is required.");
            
            var connected = ValidateConnected();
            if (!connected.IsSuccess)
                return connected;
            
            var notInRoom = ValidateNotInRoom();
            if (!notInRoom.IsSuccess)
                return notInRoom;
            
            if (_requestManager.HasPendingJoin)
                return Result.Failure("JoinRoom is already pending.");

            if (!_propertyManager.SetLocalMemberProperties(member.Id, member.DisplayName, member.Team, member.IsReady))
                return Result.Failure("Local player is unavailable.");

            _requestManager.SetPendingJoin(roomId, member);

            var joined = PhotonNetwork.JoinRoom(roomId.Value);
            if (!joined)
            {
                _requestManager.ClearPendingJoin();
                return Result.Failure("Failed to send JoinRoom request to Photon.");
            }

            return Result.Success();
        }

        public Result RequestLeaveRoom(EntityId roomId, EntityId memberId)
        {
            var inRoom = ValidateInRoom();
            if (!inRoom.IsSuccess)
                return inRoom;
            
            if (_requestManager.HasPendingLeave)
                return Result.Failure("LeaveRoom is already pending.");
            
            if (!string.Equals(PhotonNetwork.CurrentRoom.Name, roomId.Value, System.StringComparison.Ordinal))
                return Result.Failure("Current room does not match target room id.");

            _requestManager.SetPendingLeave(roomId, memberId);

            var left = PhotonNetwork.LeaveRoom();
            if (!left)
            {
                _requestManager.ClearPendingLeave();
                return Result.Failure("Failed to send LeaveRoom request to Photon.");
            }

            return Result.Success();
        }

        public Result RequestChangeTeam(EntityId roomId, EntityId memberId, TeamType team)
        {
            var inRoom = ValidateInRoom();
            if (!inRoom.IsSuccess)
                return inRoom;
            
            if (_requestManager.HasPendingChangeTeam)
                return Result.Failure("ChangeTeam is already pending.");

            var localMember = ValidateLocalMember(memberId);
            if (!localMember.IsSuccess)
                return localMember;

            _requestManager.SetPendingChangeTeam(roomId, memberId, team);

            var props = new Hashtable
            {
                ["team"] = (int)team
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            return Result.Success();
        }

        public Result RequestSetReady(EntityId roomId, EntityId memberId, bool isReady)
        {
            var inRoom = ValidateInRoom();
            if (!inRoom.IsSuccess)
                return inRoom;
            
            if (_requestManager.HasPendingSetReady)
                return Result.Failure("SetReady is already pending.");

            var localMember = ValidateLocalMember(memberId);
            if (!localMember.IsSuccess)
                return localMember;

            _requestManager.SetPendingSetReady(roomId, memberId, isReady);

            var props = new Hashtable
            {
                ["isReady"] = isReady
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
            return Result.Success();
        }

        public Result RequestStartGame(EntityId roomId)
        {
            var inRoom = ValidateInRoom();
            if (!inRoom.IsSuccess)
                return inRoom;
            
            if (!PhotonNetwork.IsMasterClient)
                return Result.Failure("Only the room master can start the game.");

            PhotonNetwork.LoadLevel(DefaultGameSceneName);
            var raised = PhotonNetwork.RaiseEvent(
                GameStartedEventCode,
                roomId.Value,
                new RaiseEventOptions { Receivers = ReceiverGroup.All },
                SendOptions.SendReliable);
            if (!raised)
                return Result.Failure("Failed to raise game started event.");
            return Result.Success();
        }

        // --- Validation Helpers ---

        private static Result ValidateConnected()
        {
            return PhotonNetwork.IsConnectedAndReady 
                ? Result.Success() 
                : Result.Failure("Photon is not connected and ready.");
        }

        private static Result ValidateNotInRoom()
        {
            return !PhotonNetwork.InRoom 
                ? Result.Success() 
                : Result.Failure("Already in a room.");
        }

        private static Result ValidateInRoom()
        {
            return PhotonNetwork.InRoom 
                ? Result.Success() 
                : Result.Failure("Not in a room.");
        }

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
