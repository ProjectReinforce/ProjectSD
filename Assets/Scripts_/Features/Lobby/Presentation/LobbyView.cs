using System.Collections;
using Features.Lobby.Application;
using Features.Lobby.Application.Events;
using Shared.EventBus;
using TMPro;
using UnityEngine;

namespace Features.Lobby.Presentation
{
    public sealed class LobbyView : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField]
        private GameObject _roomListPanel;

        [SerializeField]
        private GameObject _roomDetailPanel;

        [Header("Views")]
        [SerializeField]
        private RoomListView _roomListView;

        [SerializeField]
        private RoomDetailView _roomDetailView;

        [Header("Error")]
        [SerializeField]
        private TMP_Text _errorText;

        [SerializeField]
        private float _errorDisplayDuration = 3f;

        private IEventSubscriber _eventBus;
        private Coroutine _errorCoroutine;

        public void Initialize(IEventSubscriber eventBus, LobbyUseCases useCases)
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

            _roomListView.Initialize(useCases);
            _roomDetailView.Initialize(useCases);

            _eventBus = eventBus;
            _eventBus.Subscribe<LobbyUpdatedEvent>(this, e => RenderLobby(e.Lobby));
            _eventBus.Subscribe<RoomUpdatedEvent>(this, e => RenderRoom(e));
            _eventBus.Subscribe<GameStartedEvent>(this, e => RenderStartGame(e.Room));
            _eventBus.Subscribe<LobbyErrorEvent>(this, e => RenderError(e.Message));

            ShowRoomList();
        }

        private void OnDestroy()
        {
            _eventBus?.UnsubscribeAll(this);
        }

        public void RenderLobby(LobbySnapshot lobby)
        {
            if (_roomListView == null)
                return;
            _roomListView.Render(lobby.Rooms);
            ShowRoomList();
        }

        public void RenderRoom(RoomUpdatedEvent e)
        {
            if (_roomDetailView == null)
                return;
            _roomDetailView.SetLocalMemberId(e.LocalMemberId);
            _roomDetailView.Render(e.Room);
            ShowRoomDetail();
        }

        public void RenderStartGame(RoomSnapshot room)
        {
            Debug.Log($"[Lobby] Start game: {room.Name}");
        }

        public void RenderError(string message)
        {
            Debug.LogWarning($"[Lobby] Error: {message}");

            if (_errorText == null)
                return;

            _errorText.text = message;
            _errorText.gameObject.SetActive(true);

            if (_errorCoroutine != null)
                StopCoroutine(_errorCoroutine);
            _errorCoroutine = StartCoroutine(HideErrorAfterDelay());
        }

        private void ShowRoomList()
        {
            if (_roomListPanel != null)
                _roomListPanel.SetActive(true);
            if (_roomDetailPanel != null)
                _roomDetailPanel.SetActive(false);
        }

        private void ShowRoomDetail()
        {
            if (_roomListPanel != null)
                _roomListPanel.SetActive(false);
            if (_roomDetailPanel != null)
                _roomDetailPanel.SetActive(true);
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
