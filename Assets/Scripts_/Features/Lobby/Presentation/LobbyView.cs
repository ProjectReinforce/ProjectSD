using Features.Lobby.Application;
using UnityEngine;

namespace Features.Lobby.Presentation
{
    public sealed class LobbyView : MonoBehaviour
    {
        [SerializeField] private RoomListView _roomListView;
        [SerializeField] private RoomDetailView _roomDetailView;

        public void RenderLobby(LobbyState lobby)
        {
            if (_roomListView == null || lobby == null)
            {
                return;
            }

            _roomListView.Render(lobby.Rooms);
        }

        public void RenderRoom(RoomState room)
        {
            if (_roomDetailView == null || room == null)
            {
                return;
            }

            _roomDetailView.Render(room);
        }

        public void RenderStartGame(RoomState room)
        {
            if (room == null)
            {
                return;
            }

            Debug.Log("[Lobby] Start game: " + room.Name);
        }

        public void RenderError(string message)
        {
            Debug.LogWarning("[Lobby] " + message);
        }
    }
}
