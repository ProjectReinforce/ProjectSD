using Features.Combat.Domain;
using Features.Projectile.Application.Events;
using Features.Projectile.Application.Ports;
using Features.Projectile.Domain;
using Features.Projectile.Domain.Hit;
using Features.Projectile.Domain.Trajectory;
using Shared.EventBus;
using Shared.Kernel;
using Shared.Time;

namespace Features.Projectile.Application
{
    public sealed class SpawnProjectileUseCase
    {
        private readonly IClockPort _clock;
        private readonly IEventPublisher _eventBus;

        public SpawnProjectileUseCase(IClockPort clock, IEventPublisher eventBus)
        {
            _clock = clock;
            _eventBus = eventBus;
        }

        public Result Execute(IProjectilePhysicsPort physics, DomainEntityId ownerId, ProjectileSpec spec)
        {
            return Execute(physics, ownerId, spec, 0f, DamageType.Magical);
        }

        public Result Execute(
            IProjectilePhysicsPort physics,
            DomainEntityId ownerId,
            ProjectileSpec spec,
            float baseDamage,
            DamageType damageType
        )
        {
            var projectile = new Domain.Projectile(
                _clock.NewId(),
                ownerId,
                spec,
                baseDamage,
                damageType
            );
            var trajectory = TrajectoryFactory.Create(spec.TrajectoryType);
            var hitResolver = HitResolverFactory.Create(spec.HitType);

            physics.Spawn(projectile, trajectory, hitResolver);
            _eventBus.Publish(new ProjectileSpawnedEvent(projectile.Id, ownerId));
            return Result.Success();
        }
    }
}
