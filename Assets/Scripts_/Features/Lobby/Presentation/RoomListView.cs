using System.Collections.Generic;
using Features.Lobby.Application;
using UnityEngine;

namespace Features.Lobby.Presentation
{
    public sealed class RoomListView : MonoBehaviour
    {
        public void Render(IReadOnlyList<RoomState> rooms)
        {
            var count = rooms == null ? 0 : rooms.Count;
            Debug.Log("[Lobby] Room list updated. Count=" + count);
        }
    }
}
