using Features.Skill.Application.Events;
using Features.Zone.Presentation;
using Shared.EventBus;
using Shared.Math;
using UnityEngine;

namespace Features.Skill.Presentation
{
    public sealed class SkillCastEffectSpawner : MonoBehaviour
    {
        [Header("Fallback Prefabs (used when SkillData has no override)")]
        [SerializeField]
        private GameObject zoneEffectPrefab;

        [SerializeField]
        private GameObject targetedEffectPrefab;

        [SerializeField]
        private GameObject selfEffectPrefab;

        private IEventSubscriber _eventBus;
        private ISkillEffectPort _effectPort;

        public void Initialize(IEventSubscriber eventBus, ISkillEffectPort effectPort = null)
        {
            _eventBus = eventBus;
            _effectPort = effectPort;
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
            var prefab = ResolveEffectPrefab(e.SkillId.Value, zoneEffectPrefab);
            if (prefab == null)
                return;

            var pos = e.Position.ToVector3();
            var dir = e.Direction.ToVector3();
            var spawnPos = pos + dir * (e.Spec.Range * 0.5f);
            var go = Instantiate(prefab, spawnPos, Quaternion.identity);

            var view = go.GetComponent<ZoneView>();
            if (view != null)
            {
                view.Initialize(e.Spec.Range, e.Spec.Cooldown);
                view.SetColor(new Color(0.5f, 0.8f, 1f, 0.6f));
            }

            PlayCastSound(e.SkillId.Value, spawnPos);
        }

        private void OnTargetedRequested(TargetedRequestedEvent e)
        {
            var prefab = ResolveEffectPrefab(e.SkillId.Value, targetedEffectPrefab);
            if (prefab == null)
                return;

            var pos = e.Position.ToVector3();
            var dir = e.Direction.ToVector3();
            var spawnPos = pos + dir * 5f;
            var go = Instantiate(prefab, spawnPos, Quaternion.identity);

            var effect = go.GetComponent<TargetedCastEffect>();
            if (effect != null)
                effect.Play();

            PlayCastSound(e.SkillId.Value, spawnPos);
        }

        private void OnSelfRequested(SelfRequestedEvent e)
        {
            var prefab = ResolveEffectPrefab(e.SkillId.Value, selfEffectPrefab);
            if (prefab == null)
                return;

            var pos = e.Position.ToVector3();
            var go = Instantiate(prefab, pos, Quaternion.identity);

            var effect = go.GetComponent<SelfCastEffect>();
            if (effect != null)
                effect.Play();

            PlayCastSound(e.SkillId.Value, pos);
        }

        private GameObject ResolveEffectPrefab(string skillId, GameObject fallback)
        {
            if (_effectPort == null)
                return fallback;

            var prefab = _effectPort.GetEffectPrefab(skillId);
            return prefab != null ? prefab : fallback;
        }

        private void PlayCastSound(string skillId, Vector3 position)
        {
            if (_effectPort == null)
                return;

            var clip = _effectPort.GetCastSound(skillId);
            if (clip == null)
                return;

            AudioSource.PlayClipAtPoint(clip, position);
        }
    }
}
