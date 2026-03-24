using Features.Combat.Application.Events;
using Features.Combat.Application.Ports;
using Features.Combat.Domain;
using Shared.EventBus;
using Shared.Kernel;

namespace Features.Combat.Application
{
    public sealed class ApplyDamageUseCase
    {
        private readonly ICombatTargetPort _target;
        private readonly IEventPublisher _eventBus;

        public ApplyDamageUseCase(ICombatTargetPort target, IEventPublisher eventBus)
        {
            _target = target;
            _eventBus = eventBus;
        }

        public Result Execute(DomainEntityId targetId, float baseDamage, DamageType damageType,
            DomainEntityId attackerId = default)
        {
            if (!_target.Exists(targetId))
                return Result.Failure($"Combat target not found: {targetId.Value}");

            var defense = _target.GetDefense(targetId);
            var finalDamage = DamageRule.Calculate(baseDamage, defense, damageType);
            var damageResult = _target.ApplyDamage(targetId, finalDamage);

            _eventBus.Publish(
                new DamageAppliedEvent(
                    targetId,
                    finalDamage,
                    damageType,
                    damageResult.RemainingHealth,
                    damageResult.IsDead,
                    attackerId
                )
            );
            return Result.Success();
        }
    }
}
