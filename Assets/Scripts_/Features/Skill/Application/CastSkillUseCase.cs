using Features.Projectile.Application.Events;
using Features.Skill.Application.Events;
using Features.Skill.Application.Ports;
using Features.Skill.Domain;
using Features.Skill.Domain.Delivery;
using Shared.EventBus;
using Shared.Kernel;
using Shared.Math;

using DomainSkill = Features.Skill.Domain.Skill;

namespace Features.Skill.Application
{
    public sealed class CastSkillUseCase
    {
        private readonly IEventPublisher _eventBus;
        private readonly CooldownTracker _cooldownTracker;
        private ISkillNetworkCommandPort _network;

        public CastSkillUseCase(IEventPublisher eventBus, CooldownTracker cooldownTracker, ISkillNetworkCommandPort network = null)
        {
            _eventBus = eventBus;
            _cooldownTracker = cooldownTracker;
            _network = network;
        }

        public void SetNetwork(ISkillNetworkCommandPort network)
        {
            _network = network;
        }

        public Result Execute(DomainSkill skill, DomainEntityId casterId, float currentTime, Float3 position, Float3 direction)
        {
            var cooldownCheck = CooldownRule.CanCast(skill, currentTime, _cooldownTracker);
            if (cooldownCheck.IsFailure)
                return cooldownCheck;

            var result = skill.Delivery.Deliver(skill.Id, casterId, skill.Spec);

            int deliveryType;
            switch (result)
            {
                case ProjectileDeliveryResult pr:
                    _eventBus.Publish(new ProjectileRequestedEvent(casterId, pr.ProjectileSpec, position, direction));
                    deliveryType = 0;
                    break;
                case ZoneDeliveryResult _:
                    _eventBus.Publish(new ZoneRequestedEvent(casterId, skill.Spec, position, direction));
                    deliveryType = 1;
                    break;
                case TargetedDeliveryResult _:
                    _eventBus.Publish(new TargetedRequestedEvent(casterId, skill.Spec, position, direction));
                    deliveryType = 2;
                    break;
                case SelfDeliveryResult _:
                    _eventBus.Publish(new SelfRequestedEvent(casterId, skill.Spec, position));
                    deliveryType = 3;
                    break;
                default:
                    deliveryType = -1;
                    break;
            }

            _cooldownTracker.RecordCast(skill.Id, currentTime);
            _eventBus.Publish(new SkillCastedEvent(skill.Id, casterId, skill.Spec));

            if (_network != null)
            {
                var pr = result as ProjectileDeliveryResult;
                _network.SendSkillCasted(new SkillCastNetworkData(
                    skill.Id, casterId,
                    skill.Spec.Damage, skill.Spec.Cooldown, skill.Spec.Range,
                    deliveryType,
                    pr != null ? (int)pr.ProjectileSpec.TrajectoryType : 0,
                    pr != null ? (int)pr.ProjectileSpec.HitType : 0,
                    pr?.ProjectileSpec.Speed ?? 0f,
                    pr?.ProjectileSpec.Radius ?? 0f,
                    position.X, position.Y, position.Z,
                    direction.X, direction.Y, direction.Z));
            }

            return Result.Success();
        }
    }
}
