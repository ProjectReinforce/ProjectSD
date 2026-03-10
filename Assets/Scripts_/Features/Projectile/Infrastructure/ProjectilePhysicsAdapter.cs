using Features.Projectile.Application.Events;
using Features.Projectile.Application.Ports;
using Features.Projectile.Domain;
using Shared.EventBus;
using Shared.Kernel;
using Shared.Math;
using UnityEngine;

namespace Features.Projectile.Infrastructure
{
    public sealed class ProjectilePhysicsAdapter : MonoBehaviour, IProjectilePhysicsPort
    {
        private Domain.Projectile _projectile;
        private ITrajectory _trajectory;
        private IHitResolver _hitResolver;
        private IEventPublisher _eventBus;

        private Float3 _origin;
        private Float3 _direction;
        private float _elapsed;

        public void Initialize(IEventPublisher eventBus)
        {
            _eventBus = eventBus;
        }

        public void Spawn(Domain.Projectile projectile, ITrajectory trajectory, IHitResolver hitResolver)
        {
            _projectile = projectile;
            _trajectory = trajectory;
            _hitResolver = hitResolver;
            _origin = transform.position.ToFloat3();
            _direction = transform.forward.ToFloat3();
            _elapsed = 0f;
        }

        private void Update()
        {
            if (_projectile == null || !_projectile.IsAlive) return;

            _elapsed += Time.deltaTime;

            var input = new TrajectoryInput(
                _origin,
                transform.position.ToFloat3(),
                _direction,
                _projectile.Spec.Speed,
                Time.deltaTime,
                _elapsed,
                Float3.Zero);

            var position = _trajectory.Calculate(in input);
            transform.position = position.ToVector3();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_projectile == null || !_projectile.IsAlive) return;

            var holder = other.GetComponentInParent<EntityIdHolder>();
            if (holder == null || !holder.IsInitialized) return;

            var result = _hitResolver.Resolve(_projectile);
            result.Apply(_projectile);

            _eventBus.Publish(new ProjectileHitEvent(_projectile.Id, holder.Id));

            if (!_projectile.IsAlive)
                Destroy(gameObject);
        }

    }
}
