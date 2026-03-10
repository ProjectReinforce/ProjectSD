using System;
using Features.Lobby.Application;
using Features.Lobby.Application.Events;
using Shared.EventBus;
using UnityEngine;

namespace Features.Lobby.Presentation
{
    public sealed class LobbyView : MonoBehaviour
    {
        [SerializeField] private RoomListView _roomListView;
        [SerializeField] private RoomDetailView _roomDetailView;

        private IEventSubscriber _eventBus;
        private Action<LobbyUpdatedEvent> _onLobbyUpdated;
        private Action<RoomUpdatedEvent> _onRoomUpdated;
        private Action<GameStartedEvent> _onGameStarted;
        private Action<LobbyErrorEvent> _onLobbyError;

        public void Initialize(
            IEventSubscriber eventBus,
            CreateRoomUseCase createRoom,
            JoinRoomUseCase joinRoom,
            LeaveRoomUseCase leaveRoom,
            ChangeTeamUseCase changeTeam,
            SetReadyUseCase setReady,
            StartGameUseCase startGame)
        {
            if (_roomListView == null)
            {
                Debug.LogError("[LobbyView] _roomListView is not assigned.");
                return;
            }

            if (_roomDetailView == null)
            {
                Debug.LogError("[LobbyView] _roomDetailView is not assigned.");
                return;
            }

            _roomListView.Initialize(createRoom, joinRoom);
            _roomDetailView.Initialize(leaveRoom, changeTeam, setReady, startGame);

            _eventBus = eventBus;
            _onLobbyUpdated = e => RenderLobby(e.Lobby);
            _onRoomUpdated = e => RenderRoom(e.Room);
            _onGameStarted = e => RenderStartGame(e.Room);
            _onLobbyError = e => RenderError(e.Message);

            _eventBus.Subscribe(_onLobbyUpdated);
            _eventBus.Subscribe(_onRoomUpdated);
            _eventBus.Subscribe(_onGameStarted);
            _eventBus.Subscribe(_onLobbyError);
        }

        private void OnDestroy()
        {
            if (_eventBus == null) return;
            _eventBus.Unsubscribe(_onLobbyUpdated);
            _eventBus.Unsubscribe(_onRoomUpdated);
            _eventBus.Unsubscribe(_onGameStarted);
            _eventBus.Unsubscribe(_onLobbyError);
        }

        public void RenderLobby(LobbySnapshot lobby)
        {
            if (_roomListView == null)
            {
                Debug.LogError("[LobbyView] _roomListView is not assigned.");
                return;
            }

            _roomListView.Render(lobby.Rooms);
        }

        public void RenderRoom(RoomSnapshot room)
        {
            if (_roomDetailView == null)
            {
                Debug.LogError("[LobbyView] _roomDetailView is not assigned.");
                return;
            }

            _roomDetailView.Render(room);
        }

        public void RenderStartGame(RoomSnapshot room)
        {
            Debug.Log($"[Lobby] Start game: {room.Name}");
        }

        public void RenderError(string message)
        {
            Debug.LogWarning($"[Lobby] Error: {message}");
        }
    }
}
