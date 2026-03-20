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

        public Result Execute(DomainEntityId targetId, float baseDamage, DamageType damageType)
        {
            var defense = _target.GetDefense(targetId);
            var finalDamage = DamageRule.Calculate(baseDamage, defense, damageType);

            _target.ApplyDamage(targetId, finalDamage);
            _eventBus.Publish(new DamageAppliedEvent(targetId, finalDamage, damageType));
            return Result.Success();
        }
    }
}
