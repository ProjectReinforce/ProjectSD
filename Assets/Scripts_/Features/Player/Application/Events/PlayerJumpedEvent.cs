using Shared.Kernel;

namespace Features.Player.Application.Events
{
    public readonly struct PlayerJumpedEvent
    {
        public DomainEntityId PlayerId { get; }

        public PlayerJumpedEvent(DomainEntityId playerId)
        {
            PlayerId = playerId;
        }
    }
}
