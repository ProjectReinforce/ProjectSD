using System;
using Features.Lobby.Application.Events;
using Shared.Kernel;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using EntityId = Shared.Kernel.EntityId;

namespace Features.Lobby.Presentation
{
    public sealed class RoomItemView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _roomNameText;
        [SerializeField] private TMP_Text _memberCountText;
        [SerializeField] private Button _joinButton;

        private EntityId _roomId;
        private Action<EntityId> _onJoinClicked;

        public void Bind(RoomSnapshot room, Action<EntityId> onJoinClicked)
        {
            _roomId = room.Id;
            _onJoinClicked = onJoinClicked;

            _roomNameText.text = room.Name;
            _memberCountText.text = $"{room.Members.Count}/{room.Capacity}";

            _joinButton.onClick.RemoveAllListeners();
            _joinButton.onClick.AddListener(HandleJoinClicked);
            _joinButton.interactable = room.Members.Count < room.Capacity;
        }

        private void HandleJoinClicked()
        {
            _onJoinClicked?.Invoke(_roomId);
        }
    }
}
