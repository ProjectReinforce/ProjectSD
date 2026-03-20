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
        private readonly IProjectilePhysicsPort _physics;
        private readonly IClockPort _clock;
        private readonly IEventPublisher _eventBus;

        public SpawnProjectileUseCase(IProjectilePhysicsPort physics, IClockPort clock, IEventPublisher eventBus)
        {
            _physics = physics;
            _clock = clock;
            _eventBus = eventBus;
        }

        public Result Execute(DomainEntityId ownerId, ProjectileSpec spec)
        {
            var projectile = new Domain.Projectile(_clock.NewId(), ownerId, spec);
            var trajectory = TrajectoryFactory.Create(spec.TrajectoryType);
            var hitResolver = HitResolverFactory.Create(spec.HitType);

            _physics.Spawn(projectile, trajectory, hitResolver);
            _eventBus.Publish(new ProjectileSpawnedEvent(projectile.Id, ownerId));
            return Result.Success();
        }
    }
}
