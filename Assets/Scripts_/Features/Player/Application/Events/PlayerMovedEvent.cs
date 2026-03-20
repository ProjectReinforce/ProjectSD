using Shared.Kernel;
using Shared.Math;

namespace Features.Player.Application.Events
{
    public readonly struct PlayerMovedEvent
    {
        public DomainEntityId PlayerId { get; }
        public Float3 Position { get; }

        public PlayerMovedEvent(DomainEntityId playerId, Float3 position)
        {
            PlayerId = playerId;
            Position = position;
        }
    }
}
