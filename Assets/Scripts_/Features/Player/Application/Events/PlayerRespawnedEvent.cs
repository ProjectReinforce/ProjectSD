using Shared.Kernel;

namespace Features.Player.Application.Events
{
    public readonly struct PlayerRespawnedEvent
    {
        public DomainEntityId PlayerId { get; }
        public float CurrentHp { get; }
        public float MaxHp { get; }

        public PlayerRespawnedEvent(DomainEntityId playerId, float currentHp, float maxHp)
        {
            PlayerId = playerId;
            CurrentHp = currentHp;
            MaxHp = maxHp;
        }
    }
}
