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
            networkCallbacks.OnRemoteDamaged = HandleRemoteDamaged;
            networkCallbacks.OnRemoteDied = HandleRemoteDied;
            networkCallbacks.OnRemoteRespawned = HandleRemoteRespawned;
            networkCallbacks.OnHealthSynced = HandleHealthSynced;
        }

        private void HandleRemoteJumped(DomainEntityId playerId)
        {
            _publisher.Publish(new PlayerJumpedEvent(playerId));
        }

        private void HandleRemoteDamaged(DomainEntityId targetId, float damage,
            Features.Combat.Domain.DamageType damageType, DomainEntityId attackerId)
        {
        }

        private void HandleRemoteDied(DomainEntityId targetId, DomainEntityId killerId)
        {
            _publisher.Publish(new PlayerDiedEvent(targetId, killerId));
        }

        private void HandleRemoteRespawned(DomainEntityId targetId)
        {
        }

        private void HandleHealthSynced(DomainEntityId targetId, float currentHp, float maxHp)
        {
        }
    }
}
