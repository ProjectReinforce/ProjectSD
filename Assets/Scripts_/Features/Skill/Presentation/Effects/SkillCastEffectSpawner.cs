using Features.Skill.Application.Events;
using Features.Zone.Presentation;
using Shared.EventBus;
using Shared.Math;
using UnityEngine;

namespace Features.Skill.Presentation
{
    public sealed class SkillCastEffectSpawner : MonoBehaviour
    {
        [SerializeField]
        private GameObject zoneEffectPrefab;

        [SerializeField]
        private GameObject targetedEffectPrefab;

        [SerializeField]
        private GameObject selfEffectPrefab;

        private IEventSubscriber _eventBus;

        public void Initialize(IEventSubscriber eventBus)
        {
            ResolvePrefabs();
            _eventBus = eventBus;

            _eventBus.Subscribe(this, new System.Action<ZoneRequestedEvent>(OnZoneRequested));
            _eventBus.Subscribe(
                this,
                new System.Action<TargetedRequestedEvent>(OnTargetedRequested)
            );
            _eventBus.Subscribe(this, new System.Action<SelfRequestedEvent>(OnSelfRequested));
        }

        private void OnDestroy()
        {
            _eventBus?.UnsubscribeAll(this);
        }

        private void OnZoneRequested(ZoneRequestedEvent e)
        {
            if (zoneEffectPrefab == null)
                return;

            var pos = e.Position.ToVector3();
            var dir = e.Direction.ToVector3();
            var spawnPos = pos + dir * (e.Spec.Range * 0.5f);
            var go = Instantiate(zoneEffectPrefab, spawnPos, Quaternion.identity);

            var view = go.GetComponent<ZoneView>();
            if (view != null)
            {
                view.Initialize(e.Spec.Range, e.Spec.Cooldown);
                view.SetColor(new Color(0.5f, 0.8f, 1f, 0.6f));
            }

            Debug.Log($"[SkillCastEffectSpawner] Spawned zone: {go.name}");
        }

        private void OnTargetedRequested(TargetedRequestedEvent e)
        {
            if (targetedEffectPrefab == null)
                return;

            var pos = e.Position.ToVector3();
            var dir = e.Direction.ToVector3();
            var spawnPos = pos + dir * 5f;
            var go = Instantiate(targetedEffectPrefab, spawnPos, Quaternion.identity);

            var effect = go.GetComponent<TargetedCastEffect>();
            if (effect != null)
                effect.Play();

            Debug.Log($"[SkillCastEffectSpawner] Spawned targeted effect: {go.name}");
        }

        private void OnSelfRequested(SelfRequestedEvent e)
        {
            if (selfEffectPrefab == null)
                return;

            var pos = e.Position.ToVector3();
            var go = Instantiate(selfEffectPrefab, pos, Quaternion.identity);

            var effect = go.GetComponent<SelfCastEffect>();
            if (effect != null)
                effect.Play();

            Debug.Log($"[SkillCastEffectSpawner] Spawned self effect: {go.name}");
        }

        private void ResolvePrefabs()
        {
            zoneEffectPrefab ??= Resources.Load<GameObject>("ZoneEffect");
            targetedEffectPrefab ??= Resources.Load<GameObject>("TargetedEffect");
            selfEffectPrefab ??= Resources.Load<GameObject>("SelfEffect");
        }
    }
}
