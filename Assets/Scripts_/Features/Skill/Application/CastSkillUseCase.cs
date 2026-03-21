using Features.Projectile.Application.Events;
using Features.Skill.Application.Events;
using Features.Skill.Application.Ports;
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
        private readonly CooldownTracker _cooldownTracker;
        private readonly ISkillNetworkCommandPort _network;

        public CastSkillUseCase(IEventPublisher eventBus, CooldownTracker cooldownTracker, ISkillNetworkCommandPort network = null)
        {
            _eventBus = eventBus;
            _cooldownTracker = cooldownTracker;
            _network = network;
        }

        public Result Execute(DomainSkill skill, DomainEntityId casterId, float currentTime)
        {
            var cooldownCheck = CooldownRule.CanCast(skill, currentTime, _cooldownTracker);
            if (cooldownCheck.IsFailure)
                return cooldownCheck;

            var result = skill.Delivery.Deliver(skill.Id, casterId, skill.Spec);

            switch (result)
            {
                case ProjectileDeliveryResult pr:
                    _eventBus.Publish(new ProjectileRequestedEvent(casterId, pr.ProjectileSpec));
                    break;
                case ZoneDeliveryResult _:
                    _eventBus.Publish(new ZoneRequestedEvent(casterId, skill.Spec));
                    break;
                case TargetedDeliveryResult _:
                    _eventBus.Publish(new TargetedRequestedEvent(casterId, skill.Spec));
                    break;
                case SelfDeliveryResult _:
                    _eventBus.Publish(new SelfRequestedEvent(casterId, skill.Spec));
                    break;
            }

            _cooldownTracker.RecordCast(skill.Id, currentTime);
            _eventBus.Publish(new SkillCastedEvent(skill.Id, casterId, skill.Spec));
            _network?.SendSkillCasted(skill.Id, casterId, skill.Spec.Damage, skill.Spec.Cooldown, skill.Spec.Range);
            return Result.Success();
        }
    }
}
