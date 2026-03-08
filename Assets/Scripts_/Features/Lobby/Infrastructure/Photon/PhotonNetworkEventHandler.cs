using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using Features.Lobby.Application;
using Shared.Kernel;
using UnityEngine;

namespace Features.Lobby.Infrastructure.Photon
{
    /// <summary>
    /// Handles Photon callbacks and events.
    /// Single responsibility: processing Photon network callbacks.
    /// Lifecycle: Automatically registered when component is enabled via MonoBehaviourPunCallbacks.
    /// </summary>
    public sealed class PhotonNetworkEventHandler : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        private const string RoomDisplayNameKey = "roomDisplayName";
        private const byte GameStartedEventCode = 100;
        
        private LobbyConfirmHandler _callback;
        private PhotonRequestManager _requestManager;

        public void Initialize(LobbyConfirmHandler callback, PhotonRequestManager requestManager)
        {
            _callback = callback;
            _requestManager = requestManager;
        }

        // --- Room Creation Callbacks ---

        public override void OnCreatedRoom()
        {
            if (!_requestManager.HasPendingCreate)
            {
                Debug.LogWarning("[PhotonEventHandler] Unexpected OnCreatedRoom: no pending create.");
                return;
            }

            var pending = _requestManager.ConsumePendingCreate();
            _callback.OnRoomCreated(pending.Room);
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            if (!_requestManager.HasPendingCreate)
            {
                Debug.LogWarning("[PhotonEventHandler] Unexpected OnCreateRoomFailed: no pending create.");
                return;
            }

            _requestManager.ClearPendingCreate();
            _callback.OnNetworkError($"Create room failed ({returnCode}): {message}");
        }

        // --- Room Join Callbacks ---

        public override void OnJoinedRoom()
        {
            if (!_requestManager.HasPendingJoin)
            {
                Debug.LogWarning("[PhotonEventHandler] Unexpected OnJoinedRoom: no pending join.");
                return;
            }

            var pending = _requestManager.ConsumePendingJoin();
            _callback.OnRoomJoined(pending.RoomId, pending.Member);
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            if (!_requestManager.HasPendingJoin)
            {
                Debug.LogWarning("[PhotonEventHandler] Unexpected OnJoinRoomFailed: no pending join.");
                return;
            }

            _requestManager.ClearPendingJoin();
            _callback.OnNetworkError($"Join room failed ({returnCode}): {message}");
        }

        // --- Room Leave Callbacks ---

        public override void OnLeftRoom()
        {
            if (!_requestManager.HasPendingLeave)
            {
                Debug.LogWarning("[PhotonEventHandler] Unexpected OnLeftRoom: no pending leave.");
                return;
            }

            var pending = _requestManager.ConsumePendingLeave();
            _callback.OnRoomLeft(pending.RoomId, pending.MemberId);
        }

        // --- Player Properties Update Callbacks ---

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            if (targetPlayer != PhotonNetwork.LocalPlayer)
                return;

            if (changedProps.ContainsKey("team"))
            {
                if (!_requestManager.HasPendingChangeTeam)
                {
                    Debug.LogWarning("[PhotonEventHandler] Unexpected Team update: no pending change team.");
                }
                else
                {
                    var pending = _requestManager.ConsumePendingChangeTeam();
                    _callback.OnTeamChanged(pending.RoomId, pending.MemberId, pending.Team);
                }
            }

            if (changedProps.ContainsKey("isReady"))
            {
                if (!_requestManager.HasPendingSetReady)
                {
                    Debug.LogWarning("[PhotonEventHandler] Unexpected Ready update: no pending set ready.");
                }
                else
                {
                    var pending = _requestManager.ConsumePendingSetReady();
                    _callback.OnReadyChanged(pending.RoomId, pending.MemberId, pending.IsReady);
                }
            }
        }

        // --- Custom Event Callbacks ---

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code != GameStartedEventCode)
                return;

            if (photonEvent.CustomData is string roomIdValue && !string.IsNullOrWhiteSpace(roomIdValue))
            {
                _callback.OnGameStarted(new Shared.Kernel.EntityId(roomIdValue));
                return;
            }

            Debug.LogWarning("[PhotonEventHandler] Received GameStarted event with invalid payload.");
        }
    }
}
