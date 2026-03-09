using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using Features.Lobby.Application.Events;
using Features.Lobby.Application.Ports;
using Features.Lobby.Domain;
using Shared.EventBus;
using Shared.Kernel;
using Shared.Network;
using UnityEngine;
using Op = Features.Lobby.Infrastructure.Photon.LobbyPhotonOperation;

namespace Features.Lobby.Infrastructure.Photon
{
    /// <summary>
    /// Handles Photon callbacks and events.
    /// Single responsibility: processing Photon network callbacks.
    /// - Request/response: invokes stored callbacks from PhotonRequestManager.
    /// - Push events: directly updates repository and publishes domain events.
    /// </summary>
    public sealed class PhotonNetworkEventHandler : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        private const byte GameStartedEventCode = 100;

        private PendingCallbackTracker<LobbyPhotonOperation> _requestManager;
        private ILobbyRepository _repository;
        private IEventPublisher _publisher;

        internal void Initialize(PendingCallbackTracker<LobbyPhotonOperation> requestManager, ILobbyRepository repository, IEventPublisher publisher)
        {
            _requestManager = requestManager;
            _repository = repository;
            _publisher = publisher;
        }

        // --- Room Creation Callbacks ---

        public override void OnCreatedRoom()
        {
            if (!_requestManager.IsPending(Op.Create))
            {
                Debug.LogWarning("[PhotonEventHandler] Unexpected OnCreatedRoom: no pending create.");
                return;
            }
            _requestManager.Consume(Op.Create).OnSuccess();
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            if (!_requestManager.IsPending(Op.Create))
            {
                Debug.LogWarning("[PhotonEventHandler] Unexpected OnCreateRoomFailed: no pending create.");
                return;
            }
            _requestManager.Consume(Op.Create).OnFailure($"Create room failed ({returnCode}): {message}");
        }

        // --- Room Join Callbacks ---

        public override void OnJoinedRoom()
        {
            if (!_requestManager.IsPending(Op.Join))
            {
                Debug.LogWarning("[PhotonEventHandler] Unexpected OnJoinedRoom: no pending join.");
                return;
            }
            _requestManager.Consume(Op.Join).OnSuccess();
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            if (!_requestManager.IsPending(Op.Join))
            {
                Debug.LogWarning("[PhotonEventHandler] Unexpected OnJoinRoomFailed: no pending join.");
                return;
            }
            _requestManager.Consume(Op.Join).OnFailure($"Join room failed ({returnCode}): {message}");
        }

        // --- Room Leave Callbacks ---

        public override void OnLeftRoom()
        {
            if (!_requestManager.IsPending(Op.Leave))
            {
                Debug.LogWarning("[PhotonEventHandler] Unexpected OnLeftRoom: no pending leave.");
                return;
            }
            _requestManager.Consume(Op.Leave).OnSuccess();
        }

        // --- Player Properties Update Callbacks ---

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            if (targetPlayer == PhotonNetwork.LocalPlayer)
                HandleLocalPlayerPropertyUpdate(changedProps);
            else
                HandleRemotePlayerPropertyUpdate(targetPlayer, changedProps);
        }

        private void HandleLocalPlayerPropertyUpdate(Hashtable changedProps)
        {
            if (changedProps.ContainsKey("team"))
            {
                if (!_requestManager.IsPending(Op.ChangeTeam))
                    Debug.LogWarning("[PhotonEventHandler] Unexpected Team update: no pending change team.");
                else
                    _requestManager.Consume(Op.ChangeTeam).OnSuccess();
            }

            if (changedProps.ContainsKey("isReady"))
            {
                if (!_requestManager.IsPending(Op.SetReady))
                    Debug.LogWarning("[PhotonEventHandler] Unexpected Ready update: no pending set ready.");
                else
                    _requestManager.Consume(Op.SetReady).OnSuccess();
            }
        }

        private void HandleRemotePlayerPropertyUpdate(Player remotePlayer, Hashtable changedProps)
        {
            if (!PhotonNetwork.InRoom)
                return;

            if (!remotePlayer.CustomProperties.TryGetValue("memberId", out var memberIdRaw) || memberIdRaw is not string memberIdStr)
                return;

            var roomId = new Shared.Kernel.EntityId(PhotonNetwork.CurrentRoom.Name);
            var memberId = new Shared.Kernel.EntityId(memberIdStr);
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
                return;

            var changed = false;

            if (changedProps.ContainsKey("team") && changedProps["team"] is int teamInt)
            {
                var result = room.ChangeTeam(memberId, (TeamType)teamInt);
                if (result.IsFailure)
                    Debug.LogWarning($"[PhotonEventHandler] Remote ChangeTeam failed: {result.Error}");
                else
                    changed = true;
            }

            if (changedProps.ContainsKey("isReady") && changedProps["isReady"] is bool isReady)
            {
                var result = room.SetReady(memberId, isReady);
                if (result.IsFailure)
                    Debug.LogWarning($"[PhotonEventHandler] Remote SetReady failed: {result.Error}");
                else
                    changed = true;
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
            if (photonEvent.Code != GameStartedEventCode)
                return;

            if (photonEvent.CustomData is not string roomIdValue || string.IsNullOrWhiteSpace(roomIdValue))
            {
                Debug.LogWarning("[PhotonEventHandler] Received GameStarted event with invalid payload.");
                return;
            }

            var roomId = new Shared.Kernel.EntityId(roomIdValue);
            var lobby = _repository.LoadLobby();
            var room = lobby.FindRoom(roomId);
            if (room == null)
            {
                Debug.LogWarning("[PhotonEventHandler] GameStarted event received but room not found.");
                return;
            }

            _publisher.Publish(new GameStartedEvent(room));
        }
    }
}
