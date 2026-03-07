using System.Collections.Generic;
using Features.Lobby.Application;
using UnityEngine;

namespace Features.Lobby.Presentation
{
    public sealed class RoomListView : MonoBehaviour
    {
        public void Render(IReadOnlyList<RoomState> rooms)
        {
            Debug.Log($"[Lobby] Room list updated. Count={rooms.Count}");
        }
    }
}
