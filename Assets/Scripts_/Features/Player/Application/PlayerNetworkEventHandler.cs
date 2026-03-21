using Features.Player.Application.Events;
using Features.Player.Application.Ports;
using Shared.EventBus;
using Shared.Kernel;

namespace Features.Player.Application
{
    public sealed class PlayerNetworkEventHandler
    {
        private readonly IEventPublisher _publisher;

        public PlayerNetworkEventHandler(
            IEventPublisher publisher,
            IPlayerNetworkCallbackPort networkCallbacks
        )
        {
            _publisher = publisher;

            networkCallbacks.OnRemoteJumped = HandleRemoteJumped;
        }

        private void HandleRemoteJumped(DomainEntityId playerId)
        {
            _publisher.Publish(new PlayerJumpedEvent(playerId));
        }
    }
}
