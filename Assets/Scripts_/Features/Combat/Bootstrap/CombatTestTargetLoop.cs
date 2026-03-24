using System.Collections;
using Features.Combat.Application.Events;
using Features.Combat.Presentation;
using Shared.EventBus;
using Shared.Kernel;
using UnityEngine;

namespace Features.Combat.Bootstrap
{
    public sealed class CombatTestTargetLoop : MonoBehaviour
    {
        [SerializeField]
        private EntityIdHolder _entityIdHolder;

        [SerializeField]
        private CombatTargetView _targetView;

        [SerializeField]
        private float _respawnDelay = 1.5f;

        private IEventSubscriber _eventBus;
        private CombatBootstrap _combatBootstrap;
        private Coroutine _resetRoutine;

        public void Initialize(IEventSubscriber eventBus, CombatBootstrap combatBootstrap)
        {
            if (_entityIdHolder == null)
            {
                Debug.LogError(
                    "[CombatTestTargetLoop] EntityIdHolder is not assigned in Inspector.",
                    this
                );
                return;
            }

            if (_targetView == null)
            {
                Debug.LogError(
                    "[CombatTestTargetLoop] CombatTargetView is not assigned in Inspector.",
                    this
                );
                return;
            }

            if (combatBootstrap == null)
            {
                Debug.LogError("[CombatTestTargetLoop] CombatBootstrap is not provided.", this);
                return;
            }

            _eventBus = eventBus;
            _combatBootstrap = combatBootstrap;
            _eventBus.Subscribe(this, new System.Action<DamageAppliedEvent>(OnDamageApplied));
        }

        private void OnDestroy()
        {
            if (_resetRoutine != null)
                StopCoroutine(_resetRoutine);

            _eventBus?.UnsubscribeAll(this);
        }

        private void OnDamageApplied(DamageAppliedEvent e)
        {
            if (!_entityIdHolder.IsInitialized)
                return;

            if (!_entityIdHolder.Id.Equals(e.TargetId) || !e.IsDead)
                return;

            if (_resetRoutine != null)
                return;

            _resetRoutine = StartCoroutine(ResetAfterDelay());
        }

        private IEnumerator ResetAfterDelay()
        {
            yield return new WaitForSeconds(_respawnDelay);

            var result = _combatBootstrap.ResetTarget(_entityIdHolder.Id);
            if (result.IsFailure)
                Debug.LogWarning($"[CombatTestTargetLoop] Reset failed: {result.Error}", this);
            else
                _targetView.ResetVisual();

            _resetRoutine = null;
        }
    }
}
