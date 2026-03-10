using Features.Skill.Application.Delivery;
using Features.Skill.Domain;
using Shared.EventBus;
using Shared.Kernel;

namespace Features.Skill.Application
{
    public sealed class CastSkillUseCase
    {
        private readonly IEventPublisher _eventBus;

        public CastSkillUseCase(IEventPublisher eventBus)
        {
            _eventBus = eventBus;
        }

        public Result Execute(Skill skill, EntityId casterId, float currentTime, float lastCastTime)
        {
            var cooldownCheck = CooldownRule.CanCast(skill, currentTime, lastCastTime);
            if (cooldownCheck.IsFailure)
                return cooldownCheck;

            var delivery = DeliveryFactory.Create(skill.Spec.DeliveryType, _eventBus);
            delivery.Deliver(skill.Id, casterId, skill.Spec);
            return Result.Success();
        }
    }
}
