using Features.Combat.Application;
using Features.Combat.Domain;
using Features.Combat.Infrastructure;
using Features.Combat.Presentation;
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

        private readonly EventBus _eventBus = new EventBus();

        private ApplyDamageUseCase _applyDamage;

        private void Awake()
        {
            if (_targetAdapter == null)
            {
                Debug.LogError("[CombatBootstrap] CombatTargetAdapter is not assigned in Inspector.", this);
                return;
            }

            _targetAdapter.Initialize();
            _applyDamage = new ApplyDamageUseCase(_targetAdapter, _eventBus);

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
        }

        public Result ApplyDamage(DomainEntityId targetId, float baseDamage, DamageType damageType)
        {
            if (_applyDamage == null)
                return Result.Failure("Combat system is not initialized.");

            return _applyDamage.Execute(targetId, baseDamage, damageType);
        }

        public Result ApplyDamage(string targetIdValue, float baseDamage, DamageType damageType)
        {
            if (string.IsNullOrWhiteSpace(targetIdValue))
                return Result.Failure("Target id is required.");

            return ApplyDamage(new DomainEntityId(targetIdValue), baseDamage, damageType);
        }
    }
}
