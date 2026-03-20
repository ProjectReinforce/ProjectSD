using System;
using Features.Lobby.Application.Events;
using Features.Lobby.Application.Ports;
using Shared.Kernel;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Features.Lobby.Presentation
{
    public sealed class RoomItemView : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text _roomNameText;

        [SerializeField]
        private TMP_Text _memberCountText;

        [SerializeField]
        private Button _joinButton;

        private DomainEntityId _roomId;
        private Action<DomainEntityId> _onJoinClicked;

        public void Bind(RoomSnapshot room, Action<DomainEntityId> onJoinClicked)
        {
            Bind(room.Id, room.Name, room.Members.Count, room.Capacity, onJoinClicked);
        }

        public void Bind(RoomListItem room, Action<DomainEntityId> onJoinClicked)
        {
            Bind(room.RoomId, room.RoomName, room.PlayerCount, room.MaxPlayers, onJoinClicked);
        }

        private void Bind(
            DomainEntityId roomId,
            string name,
            int playerCount,
            int capacity,
            Action<DomainEntityId> onJoinClicked
        )
        {
            _roomId = roomId;
            _onJoinClicked = onJoinClicked;

            _roomNameText.text = name;
            _memberCountText.text = $"{playerCount}/{capacity}";

            _joinButton.onClick.RemoveAllListeners();
            _joinButton.onClick.AddListener(HandleJoinClicked);
            _joinButton.interactable = playerCount < capacity;
        }

        private void HandleJoinClicked()
        {
            _onJoinClicked?.Invoke(_roomId);
        }
    }
}
