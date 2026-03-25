using Features.Projectile.Application.Events;
using Shared.Kernel;

namespace Features.Combat.Application
{
    public sealed class CombatNetworkEventHandler
    {
        private readonly ApplyDamageUseCase _applyDamage;

        public CombatNetworkEventHandler(ApplyDamageUseCase applyDamage)
        {
            _applyDamage = applyDamage;
        }

        public Result HandleProjectileHit(ProjectileHitEvent e)
        {
            return _applyDamage.Execute(e.TargetId, e.BaseDamage, e.DamageType, e.OwnerId);
        }
    }
}
