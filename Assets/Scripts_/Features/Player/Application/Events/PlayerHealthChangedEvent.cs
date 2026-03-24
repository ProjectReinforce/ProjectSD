using Shared.Kernel;

namespace Features.Player.Application.Events
{
    public readonly struct PlayerHealthChangedEvent
    {
        public DomainEntityId PlayerId { get; }
        public float CurrentHp { get; }
        public float MaxHp { get; }
        public float DamageTaken { get; }
        public bool IsDead { get; }

        public PlayerHealthChangedEvent(
            DomainEntityId playerId,
            float currentHp,
            float maxHp,
            float damageTaken,
            bool isDead
        )
        {
            PlayerId = playerId;
            CurrentHp = currentHp;
            MaxHp = maxHp;
            DamageTaken = damageTaken;
            IsDead = isDead;
        }
    }
}
