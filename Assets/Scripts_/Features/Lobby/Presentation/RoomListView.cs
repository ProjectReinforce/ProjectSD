using Features.Lobby.Application.Events;
using Features.Lobby.Application.UseCases;
using System.Collections.Generic;
using Shared.Kernel;
using UnityEngine;

namespace Features.Lobby.Presentation
{
    public sealed class RoomListView : MonoBehaviour
    {
        private CreateRoomUseCase _createRoom;
        private JoinRoomUseCase _joinRoom;

        public void Initialize(CreateRoomUseCase createRoom, JoinRoomUseCase joinRoom)
        {
            _createRoom = createRoom;
            _joinRoom = joinRoom;
        }

        public Result CreateRoom(string roomName, int capacity, string ownerDisplayName)
            => _createRoom.Execute(roomName, capacity, ownerDisplayName);

        public Result JoinRoom(Shared.Kernel.EntityId roomId, string memberDisplayName)
            => _joinRoom.Execute(roomId, memberDisplayName);

        public void Render(IReadOnlyList<RoomSnapshot> rooms)
        {
            Debug.Log($"[Lobby] Room list updated. Count={rooms.Count}");
        }
    }
}
