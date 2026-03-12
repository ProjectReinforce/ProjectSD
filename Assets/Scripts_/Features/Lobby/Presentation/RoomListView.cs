using System.Collections.Generic;
using Features.Lobby.Application.Events;
using Features.Lobby.Application.UseCases;
using Shared.Kernel;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using EntityId = Shared.Kernel.EntityId;

namespace Features.Lobby.Presentation
{
    public sealed class RoomListView : MonoBehaviour
    {
        [Header("Create Room")]
        [SerializeField] private TMP_InputField _roomNameInput;
        [SerializeField] private TMP_InputField _capacityInput;
        [SerializeField] private TMP_InputField _displayNameInput;
        [SerializeField] private Button _createRoomButton;

        [Header("Room List")]
        [SerializeField] private Transform _roomListContent;
        [SerializeField] private RoomItemView _roomItemPrefab;

        private CreateRoomUseCase _createRoom;
        private JoinRoomUseCase _joinRoom;
        private readonly List<RoomItemView> _spawnedItems = new List<RoomItemView>();

        public void Initialize(CreateRoomUseCase createRoom, JoinRoomUseCase joinRoom)
        {
            _createRoom = createRoom;
            _joinRoom = joinRoom;

            if (_createRoomButton != null)
                _createRoomButton.onClick.AddListener(HandleCreateRoom);
        }

        public Result CreateRoom(string roomName, int capacity, string ownerDisplayName)
            => _createRoom.Execute(roomName, capacity, ownerDisplayName);

        public Result JoinRoom(EntityId roomId, string memberDisplayName)
            => _joinRoom.Execute(roomId, memberDisplayName);

        public void Render(IReadOnlyList<RoomSnapshot> rooms)
        {
            ClearList();

            if (_roomItemPrefab == null || _roomListContent == null)
            {
                Debug.Log($"[Lobby] Room list updated. Count={rooms.Count} (no prefab/content assigned)");
                return;
            }

            foreach (var room in rooms)
            {
                var item = Instantiate(_roomItemPrefab, _roomListContent);
                item.Bind(room, OnJoinRoomClicked);
                _spawnedItems.Add(item);
            }
        }

        private void HandleCreateRoom()
        {
            var roomName = _roomNameInput != null ? _roomNameInput.text : "New Room";
            var capacityText = _capacityInput != null ? _capacityInput.text : "4";
            var displayName = _displayNameInput != null ? _displayNameInput.text : "Player";

            if (!int.TryParse(capacityText, out var capacity))
                capacity = 4;

            var result = _createRoom.Execute(roomName, capacity, displayName);
            if (result.IsFailure)
                Debug.LogWarning($"[Lobby] Create room failed: {result.Error}");
        }

        private void OnJoinRoomClicked(EntityId roomId)
        {
            var displayName = _displayNameInput != null ? _displayNameInput.text : "Player";
            var result = _joinRoom.Execute(roomId, displayName);
            if (result.IsFailure)
                Debug.LogWarning($"[Lobby] Join room failed: {result.Error}");
        }

        private void ClearList()
        {
            foreach (var item in _spawnedItems)
            {
                if (item != null)
                    Destroy(item.gameObject);
            }
            _spawnedItems.Clear();
        }
    }
}
