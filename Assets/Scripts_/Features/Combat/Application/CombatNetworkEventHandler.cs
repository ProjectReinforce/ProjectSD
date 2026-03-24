using Features.Combat.Application.Ports;
using Features.Projectile.Application.Events;
using Shared.EventBus;
using Shared.Kernel;

namespace Features.Combat.Application
{
    public sealed class CombatNetworkEventHandler
    {
        private readonly ApplyDamageUseCase _applyDamage;
        private readonly IEventPublisher _publisher;

        public CombatNetworkEventHandler(
            ApplyDamageUseCase applyDamage,
            IEventPublisher publisher
        )
        {
            _applyDamage = applyDamage;
            _publisher = publisher;
        }

        public void HandleProjectileHit(ProjectileHitEvent e)
        {
            var result = _applyDamage.Execute(e.TargetId, e.BaseDamage, e.DamageType, e.OwnerId);
            if (result.IsFailure)
            {
                UnityEngine.Debug.LogWarning($"[Combat] Damage failed: {result.Error}");
            }
        }
    }
}
