using Features.Lobby.Application.Events;
using Features.Lobby.Domain;
using Shared.EventBus;
using UnityEngine;

using DomainLobby = Features.Lobby.Domain.Lobby;

namespace Features.Lobby.Presentation
{
    public sealed class LobbyView : MonoBehaviour
    {
        [SerializeField] private RoomListView _roomListView;
        [SerializeField] private RoomDetailView _roomDetailView;

        private LobbyInputHandler _inputHandler;

        public void Initialize(IEventBus eventBus)
        {
            eventBus.Subscribe<LobbyUpdatedEvent>(e => RenderLobby(e.Lobby));
            eventBus.Subscribe<RoomUpdatedEvent>(e => RenderRoom(e.Room));
            eventBus.Subscribe<GameStartedEvent>(e => RenderStartGame(e.Room));
            eventBus.Subscribe<LobbyErrorEvent>(e => RenderError(e.Message));
        }

        public void SetInputHandler(LobbyInputHandler inputHandler)
        {
            _inputHandler = inputHandler;
        }

        public void RenderLobby(DomainLobby lobby)
        {
            if (_roomListView == null)
            {
                Debug.LogError("[LobbyView] _roomListView is not assigned.");
                return;
            }

            _roomListView.Render(lobby.Rooms);
        }

        public void RenderRoom(Room room)
        {
            if (_roomDetailView == null)
            {
                Debug.LogError("[LobbyView] _roomDetailView is not assigned.");
                return;
            }

            _roomDetailView.Render(room);
        }

        public void RenderStartGame(Room room)
        {
            Debug.Log($"[Lobby] Start game: {room.Name}");
        }

        public void RenderError(string message)
        {
            Debug.LogWarning($"[Lobby] Error: {message}");
        }
    }
}
