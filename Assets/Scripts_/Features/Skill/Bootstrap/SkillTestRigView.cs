using Features.Projectile.Application;
using Features.Projectile.Application.Events;
using Features.Projectile.Infrastructure;
using Features.Skill.Application.Events;
using Shared.EventBus;
using Shared.Kernel;
using Shared.Time;
using UnityEngine;

namespace Features.Skill.Bootstrap
{
    public sealed class SkillTestRigView : MonoBehaviour
    {
        private const float ProjectileLifetimeSeconds = 6f;
        private const float DummyDistance = 8f;

        private IEventSubscriber _eventBus;
        private IEventPublisher _publisher;
        private ClockAdapter _clock;
        private Transform _spawnAnchor;

        public void Initialize(EventBus eventBus, ClockAdapter clock)
        {
            _eventBus = eventBus;
            _publisher = eventBus;
            _clock = clock;

            _eventBus.Subscribe(this, new System.Action<SkillCastedEvent>(OnSkillCasted));
            _eventBus.Subscribe(this, new System.Action<ProjectileRequestedEvent>(OnProjectileRequested));
            _eventBus.Subscribe(this, new System.Action<ProjectileSpawnedEvent>(OnProjectileSpawned));
            _eventBus.Subscribe(this, new System.Action<ProjectileHitEvent>(OnProjectileHit));

            _spawnAnchor = EnsureSpawnAnchor();
            EnsureTargetDummy();
        }

        private void OnDestroy()
        {
            _eventBus?.UnsubscribeAll(this);
        }

        private Transform EnsureSpawnAnchor()
        {
            var existing = transform.Find("CasterAnchor");
            if (existing != null) return existing;

            var go = new GameObject("CasterAnchor");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, 1f, 0f);
            go.transform.rotation = Quaternion.identity;
            return go.transform;
        }

        private void EnsureTargetDummy()
        {
            if (transform.Find("TargetDummy") != null) return;

            var dummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dummy.name = "TargetDummy";
            dummy.transform.SetParent(transform, false);
            dummy.transform.position = _spawnAnchor.position + (Vector3.forward * DummyDistance);
            dummy.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

            var holder = dummy.GetComponent<EntityIdHolder>();
            if (holder == null) holder = dummy.AddComponent<EntityIdHolder>();
            holder.Set(DomainEntityId.New());
        }

        private void OnSkillCasted(SkillCastedEvent e)
        {
            Debug.Log($"[SkillTest] Cast OK - skill={e.SkillId} caster={e.CasterId} dmg={e.Spec.Damage}");
        }

        private void OnProjectileRequested(ProjectileRequestedEvent e)
        {
            Debug.Log($"[SkillTest] Projectile requested - owner={e.OwnerId} speed={e.Spec.Speed} trajectory={e.Spec.TrajectoryType}");

            var projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileObject.name = $"TestProjectile_{e.Spec.TrajectoryType}_{e.Spec.HitType}";
            projectileObject.transform.position = _spawnAnchor.position;
            projectileObject.transform.rotation = _spawnAnchor.rotation;
            projectileObject.transform.localScale = Vector3.one * Mathf.Max(e.Spec.Radius * 2f, 0.25f);

            var collider = projectileObject.GetComponent<SphereCollider>();
            if (collider != null) collider.isTrigger = true;

            var rigidbody = projectileObject.GetComponent<Rigidbody>();
            if (rigidbody == null) rigidbody = projectileObject.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;

            var renderer = projectileObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color =
                    e.Spec.HitType == Features.Projectile.Domain.Hit.HitType.Piercing
                        ? new Color(0.35f, 0.75f, 1f)
                        : new Color(1f, 0.45f, 0.2f);
            }

            var physics = projectileObject.GetComponent<ProjectilePhysicsAdapter>();
            if (physics == null) physics = projectileObject.AddComponent<ProjectilePhysicsAdapter>();
            physics.Initialize(_publisher);

            var spawnUseCase = new SpawnProjectileUseCase(physics, _clock, _publisher);
            var result = spawnUseCase.Execute(e.OwnerId, e.Spec);
            if (result.IsFailure)
            {
                Debug.LogWarning($"[SkillTest] Projectile spawn FAILED: {result.Error}");
                Destroy(projectileObject);
                return;
            }

            Destroy(projectileObject, ProjectileLifetimeSeconds);
        }

        private void OnProjectileSpawned(ProjectileSpawnedEvent e)
        {
            Debug.Log($"[SkillTest] Projectile spawned - projectile={e.ProjectileId} owner={e.OwnerId}");
        }

        private void OnProjectileHit(ProjectileHitEvent e)
        {
            Debug.Log($"[SkillTest] Projectile hit - projectile={e.ProjectileId} target={e.TargetId}");
        }
    }
}
