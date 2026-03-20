using Shared.Kernel;

namespace Features.Player.Application.Events
{
    public readonly struct PlayerSpawnedEvent
    {
        public DomainEntityId PlayerId { get; }

        public PlayerSpawnedEvent(DomainEntityId playerId)
        {
            PlayerId = playerId;
        }
    }
}
