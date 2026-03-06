using Features.Lobby.Application;
using UnityEngine;

namespace Features.Lobby.Presentation
{
    public sealed class RoomDetailView : MonoBehaviour
    {
        public void Render(RoomState room)
        {
            if (room == null)
            {
                return;
            }

            Debug.Log("[Lobby] Room detail updated: " + room.Name + " (" + room.Members.Count + "/" + room.Capacity + ")");
        }
    }
}
