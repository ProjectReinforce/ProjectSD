using Features.Combat.Domain;
using Features.Projectile.Application.Events;
using Features.Projectile.Domain;
using Features.Projectile.Domain.Hit;
using Features.Projectile.Domain.Trajectory;
using Features.Skill.Application.Events;
using Features.Skill.Application.Ports;
using Features.Skill.Domain;
using Features.Skill.Domain.Delivery;
using Shared.EventBus;

namespace Features.Skill.Application
{
    public sealed class SkillNetworkEventHandler
    {
        private readonly IEventPublisher _publisher;

        public SkillNetworkEventHandler(
            IEventPublisher publisher,
            ISkillNetworkCallbackPort networkCallbacks
        )
        {
            _publisher = publisher;
            networkCallbacks.OnSkillCasted = HandleSkillCasted;
        }

        private void HandleSkillCasted(SkillCastNetworkData data)
        {
            var spec = new SkillSpec(data.Damage, data.Cooldown, data.Range);

            switch (data.DeliveryType)
            {
                case DeliveryType.Projectile:
                    var projectileSpec = new ProjectileSpec(
                        (TrajectoryType)data.TrajectoryType,
                        (HitType)data.HitType,
                        data.Speed, data.Radius);
                    _publisher.Publish(
                        new ProjectileRequestedEvent(
                            data.CasterId,
                            projectileSpec,
                            data.Damage,
                            DamageType.Magical,
                            data.Position,
                            data.Direction
                        )
                    );
                    break;
                case DeliveryType.Zone:
                    _publisher.Publish(new ZoneRequestedEvent(data.SkillId, data.CasterId, spec, data.Position, data.Direction));
                    break;
                case DeliveryType.Targeted:
                    _publisher.Publish(new TargetedRequestedEvent(data.SkillId, data.CasterId, spec, data.Position, data.Direction));
                    break;
                case DeliveryType.Self:
                    _publisher.Publish(new SelfRequestedEvent(data.SkillId, data.CasterId, spec, data.Position));
                    break;
            }

            _publisher.Publish(new SkillCastedEvent(data.SkillId, data.CasterId, data.SlotIndex, spec));
        }
    }
}
