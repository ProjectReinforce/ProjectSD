using Features.Combat.Application;
using Features.Combat.Application.Ports;
using Features.Combat.Domain;
using Features.Combat.Infrastructure;
using Features.Combat.Presentation;
using Features.Projectile.Application.Events;
using Shared.EventBus;
using Shared.Kernel;
using UnityEngine;

namespace Features.Combat.Bootstrap
{
    public sealed class CombatBootstrap : MonoBehaviour
    {
        [SerializeField]
        private CombatTargetAdapter _targetAdapter;

        [SerializeField]
        private CombatTargetView[] _targetViews = new CombatTargetView[0];

        [SerializeField]
        private CombatTestTargetLoop[] _testTargetLoops = new CombatTestTargetLoop[0];

        private ApplyDamageUseCase _applyDamage;
        private EventBus _eventBus;

        public void Initialize(EventBus eventBus)
        {
            if (_targetAdapter == null)
            {
                Debug.LogError("[CombatBootstrap] CombatTargetAdapter is not assigned in Inspector.", this);
                return;
            }

            if (eventBus == null)
            {
                Debug.LogError("[CombatBootstrap] EventBus is not provided.", this);
                return;
            }

            _eventBus = eventBus;

            _targetAdapter.Initialize();
            _applyDamage = new ApplyDamageUseCase(_targetAdapter, _eventBus);
            _eventBus.Subscribe(this, new System.Action<ProjectileHitEvent>(OnProjectileHit));

            for (var i = 0; i < _targetViews.Length; i++)
            {
                var view = _targetViews[i];
                if (view == null)
                {
                    Debug.LogError($"[CombatBootstrap] CombatTargetView at index {i} is null.", this);
                    continue;
                }

                view.Initialize(_eventBus);
            }

            for (var i = 0; i < _testTargetLoops.Length; i++)
            {
                var loop = _testTargetLoops[i];
                if (loop == null)
                {
                    Debug.LogError($"[CombatBootstrap] CombatTestTargetLoop at index {i} is null.", this);
                    continue;
                }

                loop.Initialize(_eventBus, this);
            }
        }

        private void OnDestroy()
        {
            _eventBus?.UnsubscribeAll(this);
        }

        private void OnProjectileHit(ProjectileHitEvent e)
        {
            if (_applyDamage == null)
            {
                Debug.LogError("[CombatBootstrap] Received ProjectileHitEvent before combat initialization.", this);
                return;
            }

            var result = _applyDamage.Execute(e.TargetId, e.BaseDamage, e.DamageType, e.OwnerId);
            if (result.IsFailure)
                Debug.LogWarning($"[CombatBootstrap] Failed to apply projectile damage: {result.Error}", this);
        }

        public void RegisterTarget(DomainEntityId targetId, ICombatTargetProvider provider)
        {
            _targetAdapter.Register(targetId, provider);
        }

        public Result ApplyDamage(DomainEntityId targetId, float baseDamage, DamageType damageType,
            DomainEntityId attackerId = default)
        {
            if (_applyDamage == null)
                return Result.Failure("Combat system is not initialized.");

            return _applyDamage.Execute(targetId, baseDamage, damageType, attackerId);
        }

        public Result ApplyDamage(string targetIdValue, float baseDamage, DamageType damageType)
        {
            if (string.IsNullOrWhiteSpace(targetIdValue))
                return Result.Failure("Target id is required.");

            return ApplyDamage(new DomainEntityId(targetIdValue), baseDamage, damageType);
        }

        public Result ResetTarget(DomainEntityId targetId)
        {
            if (_targetAdapter == null)
                return Result.Failure("Combat target adapter is not initialized.");

            return _targetAdapter.ResetTarget(targetId)
                ? Result.Success()
                : Result.Failure($"Combat target not found: {targetId.Value}");
        }
    }
}
