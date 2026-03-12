using Features.Projectile.Application.Events;
using Features.Skill.Application.Events;
using Features.Skill.Domain;
using Features.Skill.Domain.Delivery;
using Shared.EventBus;
using Shared.Kernel;

using DomainSkill = Features.Skill.Domain.Skill;

namespace Features.Skill.Application
{
    public sealed class CastSkillUseCase
    {
        private readonly IEventPublisher _eventBus;

        public CastSkillUseCase(IEventPublisher eventBus)
        {
            _eventBus = eventBus;
        }

        public Result Execute(DomainSkill skill, EntityId casterId, float currentTime, float lastCastTime)
        {
            var cooldownCheck = CooldownRule.CanCast(skill, currentTime, lastCastTime);
            if (cooldownCheck.IsFailure)
                return cooldownCheck;

            var result = skill.Delivery.Deliver(skill.Id, casterId, skill.Spec);

            if (result is ProjectileDeliveryResult pr)
                _eventBus.Publish(new ProjectileRequestedEvent(casterId, pr.ProjectileSpec));

            _eventBus.Publish(new SkillCastedEvent(skill.Id, casterId, skill.Spec));
            return Result.Success();
        }
    }
}
