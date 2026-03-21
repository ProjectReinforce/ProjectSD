using Features.Player.Application.Events;
using Shared.EventBus;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Features.Player.Presentation
{
    public sealed class PlayerView : MonoBehaviour
    {
        private IEventSubscriber _eventBus;

        public void Initialize(bool isLocal, IEventSubscriber eventBus)
        {
            _eventBus = eventBus;

            if (!isLocal)
            {
                DisableLocalComponents();
                return;
            }

            _eventBus.Subscribe(this, new System.Action<PlayerMovedEvent>(OnPlayerMoved));
            _eventBus.Subscribe(this, new System.Action<PlayerJumpedEvent>(OnPlayerJumped));
        }

        private void DisableLocalComponents()
        {
            var inputHandler = GetComponent<PlayerInputHandler>();
            if (inputHandler != null)
                inputHandler.enabled = false;

            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
                playerInput.enabled = false;
        }

        private void OnDestroy()
        {
            _eventBus?.UnsubscribeAll(this);
        }

        private void OnPlayerMoved(PlayerMovedEvent e)
        {
            // 향후 애니메이션 연동용. 지금은 no-op.
        }

        private void OnPlayerJumped(PlayerJumpedEvent e)
        {
            Debug.Log($"[Player] Jumped: {e.PlayerId}");
        }
    }
}
