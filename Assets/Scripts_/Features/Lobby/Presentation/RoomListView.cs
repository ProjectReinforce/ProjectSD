using System.Collections.Generic;
using Features.Lobby.Domain;
using UnityEngine;

namespace Features.Lobby.Presentation
{
    public sealed class RoomListView : MonoBehaviour
    {
        public void Render(IReadOnlyList<Room> rooms)
        {
            Debug.Log($"[Lobby] Room list updated. Count={rooms.Count}");
        }
    }
}
