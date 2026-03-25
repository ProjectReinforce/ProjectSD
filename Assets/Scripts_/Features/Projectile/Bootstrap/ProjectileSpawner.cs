using Features.Projectile.Application;
using Features.Projectile.Application.Events;
using Features.Projectile.Application.Ports;
using Features.Projectile.Domain;
using Features.Projectile.Domain.Hit;
using Features.Projectile.Domain.Trajectory;
using Features.Projectile.Infrastructure;
using Features.Projectile.Presentation;
using Shared.EventBus;
using Shared.Kernel;
using Shared.Math;
using Shared.Time;
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

        [SerializeField]
        private Transform _spawnRoot;

        private IEventSubscriber _eventBus;
        private IEventPublisher _publisher;
        private SpawnProjectileUseCase _spawnUseCase;

        public void Initialize(IEventSubscriber eventBus, IEventPublisher publisher)
        {
            _eventBus = eventBus;
            _publisher = publisher;
            _spawnUseCase = new SpawnProjectileUseCase(new ClockAdapter(), _publisher);

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
            var rotation = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir) : Quaternion.identity;
            var go = Instantiate(prefab, pos + dir, rotation, _spawnRoot);

            var physicsAdapter = go.GetComponent<ProjectilePhysicsAdapter>();
            if (physicsAdapter == null)
            {
                Debug.LogError("[ProjectileSpawner] ProjectilePhysicsAdapter missing on prefab.", go);
                return;
            }

            physicsAdapter.Initialize(_publisher);
            _spawnUseCase.Execute(physicsAdapter, e.OwnerId, e.Spec, e.BaseDamage, e.DamageType);

            var view = go.GetComponent<ProjectileView>();
            if (view == null)
                Debug.LogWarning("[ProjectileSpawner] ProjectileView missing on prefab.", go);
            else
                view.SetColor(GetColor(e.Spec.TrajectoryType));

            Debug.Log($"[ProjectileSpawner] Spawned: {go.name}");
        }

        private GameObject PickPrefab(ProjectileSpec spec)
        {
            switch (spec.TrajectoryType)
            {
                case TrajectoryType.Parabolic:
                    return arcBoltPrefab ?? fireballPrefab;
                case TrajectoryType.Homing:
                    return homingOrbPrefab ?? fireballPrefab;
                default:
                    return spec.Speed >= 25f ? iceLancePrefab : fireballPrefab;
            }
        }

        private static Color GetColor(TrajectoryType type)
        {
            return type switch
            {
                TrajectoryType.Parabolic => new Color(1f, 0.95f, 0.2f),
                TrajectoryType.Homing => new Color(0.6f, 0.2f, 0.9f),
                _ => new Color(1f, 0.5f, 0.1f)
            };
        }
    }
}
