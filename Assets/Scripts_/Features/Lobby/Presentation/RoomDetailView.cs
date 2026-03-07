using Features.Lobby.Domain;
using UnityEngine;

namespace Features.Lobby.Presentation
{
    public sealed class RoomDetailView : MonoBehaviour
    {
        public void Render(Room room)
        {
            Debug.Log($"[Lobby] Room detail updated: {room.Name} ({room.Members.Count}/{room.Capacity})");
        }
    }
}
