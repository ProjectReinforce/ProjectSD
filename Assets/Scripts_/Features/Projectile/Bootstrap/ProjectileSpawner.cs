using Features.Projectile.Application.Events;
using Features.Projectile.Domain;
using Features.Projectile.Domain.Hit;
using Features.Projectile.Domain.Trajectory;
using Features.Projectile.Infrastructure;
using Features.Projectile.Presentation;
using Shared.EventBus;
using Shared.Kernel;
using Shared.Math;
using UnityEngine;

namespace Features.Projectile.Bootstrap
{
    public sealed class ProjectileSpawner : MonoBehaviour
    {
        [SerializeField]
        private GameObject fireballPrefab;

        [SerializeField]
        private GameObject iceLancePrefab;

        [SerializeField]
        private GameObject arcBoltPrefab;

        [SerializeField]
        private GameObject homingOrbPrefab;

        private IEventSubscriber _eventBus;
        private IEventPublisher _publisher;

        public void Initialize(IEventSubscriber eventBus, IEventPublisher publisher)
        {
            _eventBus = eventBus;
            _publisher = publisher;

            _eventBus.Subscribe(
                this,
                new System.Action<ProjectileRequestedEvent>(OnProjectileRequested)
            );
        }

        private void OnDestroy()
        {
            _eventBus?.UnsubscribeAll(this);
        }

        private void OnProjectileRequested(ProjectileRequestedEvent e)
        {
            var prefab = PickPrefab(e.Spec);
            if (prefab == null)
            {
                Debug.LogWarning("[ProjectileSpawner] No prefab for: " + e.Spec.TrajectoryType);
                return;
            }

            var pos = e.Position.ToVector3();
            var dir = e.Direction.ToVector3();
            var rotation =
                dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir) : Quaternion.identity;
            var go = Instantiate(prefab, pos + dir, rotation);

            var adapter = go.GetComponent<ProjectilePhysicsAdapter>();
            if (adapter != null)
            {
                adapter.Initialize(_publisher);
                var projectile = new Domain.Projectile(
                    DomainEntityId.New(),
                    e.OwnerId,
                    e.Spec,
                    e.BaseDamage,
                    e.DamageType
                );
                var trajectory = TrajectoryFactory.Create(e.Spec.TrajectoryType);
                var hitResolver = HitResolverFactory.Create(e.Spec.HitType);
                adapter.Spawn(projectile, trajectory, hitResolver);
            }

            var view = go.GetComponent<ProjectileView>();
            if (view != null)
                view.SetColor(GetColor(e.Spec.TrajectoryType));

            Debug.Log($"[ProjectileSpawner] Spawned: {go.name}");
        }

        private GameObject PickPrefab(ProjectileSpec spec)
        {
            switch (spec.TrajectoryType)
            {
                case TrajectoryType.Parabolic:
                    return arcBoltPrefab != null ? arcBoltPrefab : fireballPrefab;
                case TrajectoryType.Homing:
                    return homingOrbPrefab != null ? homingOrbPrefab : fireballPrefab;
                default:
                    return spec.Speed >= 25f ? iceLancePrefab : fireballPrefab;
            }
        }

        private static Color GetColor(TrajectoryType type)
        {
            switch (type)
            {
                case TrajectoryType.Parabolic:
                    return new Color(1f, 0.95f, 0.2f);
                case TrajectoryType.Homing:
                    return new Color(0.6f, 0.2f, 0.9f);
                default:
                    return new Color(1f, 0.5f, 0.1f);
            }
        }
    }
}
