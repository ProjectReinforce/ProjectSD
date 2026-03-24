using Features.Combat.Application.Events;
using Shared.EventBus;
using Shared.Kernel;
using UnityEngine;

namespace Features.Combat.Presentation
{
    public sealed class CombatTargetView : MonoBehaviour
    {
        [SerializeField]
        private EntityIdHolder _entityIdHolder;

        [SerializeField]
        private Renderer _renderer;

        [SerializeField]
        private Color _damageFlashColor = new Color(1f, 0.3f, 0.3f, 1f);

        [SerializeField]
        private Color _deadColor = new Color(0.2f, 0.2f, 0.2f, 1f);

        [SerializeField]
        private float _flashDuration = 0.15f;

        private IEventSubscriber _eventBus;
        private Color _baseColor;
        private float _flashRemaining;
        private bool _hasBaseColor;

        public void Initialize(IEventSubscriber eventBus)
        {
            if (_entityIdHolder == null)
            {
                Debug.LogError(
                    "[CombatTargetView] EntityIdHolder is not assigned in Inspector.",
                    this
                );
                return;
            }

            _eventBus = eventBus;
            _eventBus.Subscribe(this, new System.Action<DamageAppliedEvent>(OnDamageApplied));

            if (_renderer != null)
            {
                _baseColor = _renderer.material.color;
                _hasBaseColor = true;
            }
        }

        public void ResetVisual()
        {
            _flashRemaining = 0f;
            if (_renderer != null && _hasBaseColor)
                _renderer.material.color = _baseColor;
        }

        private void Update()
        {
            if (!_hasBaseColor || _flashRemaining <= 0f)
                return;

            _flashRemaining -= Time.deltaTime;
            if (_flashRemaining <= 0f && _renderer != null)
                _renderer.material.color = _baseColor;
        }

        private void OnDestroy()
        {
            _eventBus?.UnsubscribeAll(this);
        }

        private void OnDamageApplied(DamageAppliedEvent e)
        {
            if (_entityIdHolder == null || !_entityIdHolder.IsInitialized)
                return;

            if (!_entityIdHolder.Id.Equals(e.TargetId))
                return;

            if (_renderer != null)
            {
                _renderer.material.color = e.IsDead ? _deadColor : _damageFlashColor;
                _flashRemaining = e.IsDead ? 0f : _flashDuration;
            }

            Debug.Log(
                $"[CombatTargetView] Target={e.TargetId.Value}, Damage={e.Damage}, Remaining={e.RemainingHealth}"
            );
        }
    }
}
