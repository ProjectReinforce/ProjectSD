using Shared.Kernel;

namespace Features.Player.Application.Events
{
    public readonly struct PlayerDiedEvent
    {
        public DomainEntityId PlayerId { get; }
        public DomainEntityId AttackerId { get; }

        public PlayerDiedEvent(DomainEntityId playerId, DomainEntityId attackerId)
        {
            PlayerId = playerId;
            AttackerId = attackerId;
        }
    }
}
