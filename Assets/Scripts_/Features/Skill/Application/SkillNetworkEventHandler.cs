using Features.Projectile.Application.Events;
using Features.Projectile.Domain;
using Features.Projectile.Domain.Hit;
using Features.Projectile.Domain.Trajectory;
using Features.Skill.Application.Events;
using Features.Skill.Application.Ports;
using Features.Skill.Domain;
using Shared.EventBus;
using Shared.Math;

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
            networkCallbacks.OnRemoteSkillCasted = HandleRemoteSkillCasted;
        }

        private void HandleRemoteSkillCasted(SkillCastNetworkData data)
        {
            var spec = new SkillSpec(data.Damage, data.Cooldown, data.Range);
            var position = new Float3(data.PosX, data.PosY, data.PosZ);
            var direction = new Float3(data.DirX, data.DirY, data.DirZ);

            switch (data.DeliveryType)
            {
                case 0:
                    var projectileSpec = new ProjectileSpec(
                        (TrajectoryType)data.TrajectoryType,
                        (HitType)data.HitType,
                        data.Speed, data.Radius);
                    _publisher.Publish(new ProjectileRequestedEvent(data.CasterId, projectileSpec, position, direction));
                    break;
                case 1:
                    _publisher.Publish(new ZoneRequestedEvent(data.CasterId, spec, position, direction));
                    break;
                case 2:
                    _publisher.Publish(new TargetedRequestedEvent(data.CasterId, spec, position, direction));
                    break;
                case 3:
                    _publisher.Publish(new SelfRequestedEvent(data.CasterId, spec, position));
                    break;
            }

            _publisher.Publish(new SkillCastedEvent(data.SkillId, data.CasterId, spec));
        }
    }
}
