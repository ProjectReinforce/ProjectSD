using System.Collections;
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
        private const float ZoneLifetimeSeconds = 3f;
        private const float FlashDurationSeconds = 0.5f;
        private const float DummyDistance = 8f;

        private IEventSubscriber _eventBus;
        private IEventPublisher _publisher;
        private ClockAdapter _clock;
        private Transform _spawnAnchor;
        private GameObject _targetDummy;

        public void Initialize(EventBus eventBus, ClockAdapter clock)
        {
            _eventBus = eventBus;
            _publisher = eventBus;
            _clock = clock;

            _eventBus.Subscribe(this, new System.Action<SkillCastedEvent>(OnSkillCasted));
            _eventBus.Subscribe(this, new System.Action<ProjectileRequestedEvent>(OnProjectileRequested));
            _eventBus.Subscribe(this, new System.Action<ProjectileSpawnedEvent>(OnProjectileSpawned));
            _eventBus.Subscribe(this, new System.Action<ProjectileHitEvent>(OnProjectileHit));
            _eventBus.Subscribe(this, new System.Action<ZoneRequestedEvent>(OnZoneRequested));
            _eventBus.Subscribe(this, new System.Action<TargetedRequestedEvent>(OnTargetedRequested));
            _eventBus.Subscribe(this, new System.Action<SelfRequestedEvent>(OnSelfRequested));

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
            var existing = transform.Find("TargetDummy");
            if (existing != null)
            {
                _targetDummy = existing.gameObject;
                return;
            }

            _targetDummy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _targetDummy.name = "TargetDummy";
            _targetDummy.transform.SetParent(transform, false);
            _targetDummy.transform.position = _spawnAnchor.position + (Vector3.forward * DummyDistance);
            _targetDummy.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

            var holder = _targetDummy.GetComponent<EntityIdHolder>();
            if (holder == null) holder = _targetDummy.AddComponent<EntityIdHolder>();
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

        private void OnZoneRequested(ZoneRequestedEvent e)
        {
            Debug.Log($"[SkillTest] Zone requested - caster={e.CasterId} dmg={e.Spec.Damage} range={e.Spec.Range}");

            var zone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            zone.name = "TestZone";
            zone.transform.position = _spawnAnchor.position + (Vector3.forward * DummyDistance * 0.5f);
            zone.transform.localScale = new Vector3(e.Spec.Range, 0.05f, e.Spec.Range);

            var collider = zone.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;

            var renderer = zone.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.3f, 0.5f, 1f, 0.4f);

            Destroy(zone, ZoneLifetimeSeconds);
        }

        private void OnTargetedRequested(TargetedRequestedEvent e)
        {
            Debug.Log($"[SkillTest] Targeted requested - caster={e.CasterId} dmg={e.Spec.Damage}");

            if (_targetDummy != null)
                StartCoroutine(FlashColor(_targetDummy, new Color(1f, 0.2f, 0.2f)));
        }

        private void OnSelfRequested(SelfRequestedEvent e)
        {
            Debug.Log($"[SkillTest] Self requested - caster={e.CasterId} dmg={e.Spec.Damage}");

            if (_spawnAnchor != null)
            {
                var anchor = _spawnAnchor.gameObject;
                var renderer = anchor.GetComponent<Renderer>();
                if (renderer == null)
                {
                    var filter = anchor.GetComponent<MeshFilter>();
                    if (filter == null)
                    {
                        filter = anchor.AddComponent<MeshFilter>();
                        filter.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
                    }
                    renderer = anchor.AddComponent<MeshRenderer>();
                    anchor.transform.localScale = Vector3.one * 0.5f;
                }
                StartCoroutine(FlashColor(anchor, new Color(0.3f, 1f, 0.4f)));
            }
        }

        private IEnumerator FlashColor(GameObject target, Color flashColor)
        {
            var renderer = target.GetComponent<Renderer>();
            if (renderer == null) yield break;

            var originalColor = renderer.material.color;
            renderer.material.color = flashColor;
            yield return new WaitForSeconds(FlashDurationSeconds);
            if (renderer != null)
                renderer.material.color = originalColor;
        }
    }
}
