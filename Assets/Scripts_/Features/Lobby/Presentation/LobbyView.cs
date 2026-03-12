using System;
using System.Collections;
using Features.Lobby.Application.Events;
using Features.Lobby.Application.UseCases;
using Shared.EventBus;
using TMPro;
using UnityEngine;

namespace Features.Lobby.Presentation
{
    public sealed class LobbyView : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject _roomListPanel;
        [SerializeField] private GameObject _roomDetailPanel;

        [Header("Views")]
        [SerializeField] private RoomListView _roomListView;
        [SerializeField] private RoomDetailView _roomDetailView;

        [Header("Error")]
        [SerializeField] private TMP_Text _errorText;
        [SerializeField] private float _errorDisplayDuration = 3f;

        private IEventSubscriber _eventBus;
        private Action<LobbyUpdatedEvent> _onLobbyUpdated;
        private Action<RoomUpdatedEvent> _onRoomUpdated;
        private Action<GameStartedEvent> _onGameStarted;
        private Action<LobbyErrorEvent> _onLobbyError;
        private Coroutine _errorCoroutine;

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

            ShowRoomList();
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
            if (_roomListView == null) return;
            _roomListView.Render(lobby.Rooms);
            ShowRoomList();
        }

        public void RenderRoom(RoomSnapshot room)
        {
            if (_roomDetailView == null) return;
            _roomDetailView.Render(room);
            ShowRoomDetail();
        }

        public void RenderStartGame(RoomSnapshot room)
        {
            Debug.Log($"[Lobby] Start game: {room.Name}");
        }

        public void RenderError(string message)
        {
            Debug.LogWarning($"[Lobby] Error: {message}");

            if (_errorText == null) return;

            _errorText.text = message;
            _errorText.gameObject.SetActive(true);

            if (_errorCoroutine != null)
                StopCoroutine(_errorCoroutine);
            _errorCoroutine = StartCoroutine(HideErrorAfterDelay());
        }

        private void ShowRoomList()
        {
            if (_roomListPanel != null) _roomListPanel.SetActive(true);
            if (_roomDetailPanel != null) _roomDetailPanel.SetActive(false);
        }

        private void ShowRoomDetail()
        {
            if (_roomListPanel != null) _roomListPanel.SetActive(false);
            if (_roomDetailPanel != null) _roomDetailPanel.SetActive(true);
        }

        private IEnumerator HideErrorAfterDelay()
        {
            yield return new WaitForSeconds(_errorDisplayDuration);
            if (_errorText != null)
                _errorText.gameObject.SetActive(false);
            _errorCoroutine = null;
        }
    }
}
