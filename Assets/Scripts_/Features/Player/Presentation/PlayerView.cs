using Features.Player.Application.Events;
using Shared.EventBus;
using UnityEngine;

namespace Features.Player.Presentation
{
    public sealed class PlayerView : MonoBehaviour
    {
        private IEventSubscriber _eventBus;

        public void Initialize(IEventSubscriber eventBus)
        {
            _eventBus = eventBus;
            _eventBus.Subscribe(this, new System.Action<PlayerMovedEvent>(OnPlayerMoved));
            _eventBus.Subscribe(this, new System.Action<PlayerJumpedEvent>(OnPlayerJumped));
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
