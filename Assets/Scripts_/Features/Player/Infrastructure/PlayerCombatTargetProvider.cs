using Features.Combat.Application.Ports;

namespace Features.Player.Infrastructure
{
    public sealed class PlayerCombatTargetProvider : ICombatTargetProvider
    {
        private readonly Domain.Player _player;

        public PlayerCombatTargetProvider(Domain.Player player)
        {
            _player = player;
        }

        public float GetDefense() => _player.Spec.Defense;

        public float GetCurrentHealth() => _player.CurrentHp;

        public CombatTargetDamageResult ApplyDamage(float damage)
        {
            if (_player.IsInvulnerable || _player.IsDead)
                return new CombatTargetDamageResult(_player.CurrentHp, _player.IsDead);

            var remaining = _player.TakeDamage(damage);
            return new CombatTargetDamageResult(remaining, _player.IsDead);
        }
    }
}
