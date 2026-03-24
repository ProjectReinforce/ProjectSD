using System;
using System.Collections.Generic;
using Features.Combat.Application.Ports;
using Features.Combat.Domain;
using Shared.Kernel;
using UnityEngine;

namespace Features.Combat.Infrastructure
{
    public sealed class CombatTargetAdapter : MonoBehaviour, ICombatTargetPort
    {
        [SerializeField]
        private CombatTargetConfig[] _targets = new CombatTargetConfig[0];

        private readonly Dictionary<DomainEntityId, ICombatTargetProvider> _providers =
            new Dictionary<DomainEntityId, ICombatTargetProvider>();

        private readonly Dictionary<DomainEntityId, CombatTarget> _resettableTargets =
            new Dictionary<DomainEntityId, CombatTarget>();

        public void Initialize()
        {
            _providers.Clear();
            _resettableTargets.Clear();

            for (var i = 0; i < _targets.Length; i++)
            {
                var config = _targets[i];
                if (config.EntityIdHolder == null)
                {
                    Debug.LogError($"[CombatTargetAdapter] EntityIdHolder is missing at index {i}.", this);
                    continue;
                }

                if (!config.EntityIdHolder.IsInitialized)
                    config.EntityIdHolder.Set(DomainEntityId.New());

                var targetId = config.EntityIdHolder.Id;
                if (_providers.ContainsKey(targetId))
                {
                    Debug.LogError($"[CombatTargetAdapter] Duplicate target id: {targetId.Value}", this);
                    continue;
                }

                var target = new CombatTarget(
                    targetId,
                    config.MaxHealth,
                    config.StartingHealth,
                    config.Defense
                );

                _resettableTargets.Add(targetId, target);
                _providers.Add(targetId, new CombatTargetWrapper(target));
            }
        }

        public void Register(DomainEntityId id, ICombatTargetProvider provider)
        {
            _providers[id] = provider;
        }

        public void Unregister(DomainEntityId id)
        {
            _providers.Remove(id);
        }

        public bool Exists(DomainEntityId targetId)
        {
            return _providers.ContainsKey(targetId);
        }

        public float GetDefense(DomainEntityId targetId)
        {
            if (!_providers.TryGetValue(targetId, out var provider))
            {
                Debug.LogError($"[CombatTargetAdapter] Target not found: {targetId.Value}", this);
                return 0f;
            }

            return provider.GetDefense();
        }

        public CombatTargetDamageResult ApplyDamage(DomainEntityId targetId, float damage)
        {
            if (!_providers.TryGetValue(targetId, out var provider))
            {
                Debug.LogError($"[CombatTargetAdapter] Target not found: {targetId.Value}", this);
                return new CombatTargetDamageResult(0f, false);
            }

            return provider.ApplyDamage(damage);
        }

        public bool ResetTarget(DomainEntityId targetId)
        {
            if (!_resettableTargets.TryGetValue(targetId, out var target))
            {
                Debug.LogError($"[CombatTargetAdapter] Resettable target not found: {targetId.Value}", this);
                return false;
            }

            target.Reset();
            return true;
        }

        private sealed class CombatTargetWrapper : ICombatTargetProvider
        {
            private readonly CombatTarget _target;

            public CombatTargetWrapper(CombatTarget target) => _target = target;

            public float GetDefense() => _target.Defense;
            public float GetCurrentHealth() => _target.CurrentHealth;

            public CombatTargetDamageResult ApplyDamage(float damage)
            {
                var remaining = _target.ApplyDamage(damage);
                return new CombatTargetDamageResult(remaining, _target.IsDead);
            }
        }

        [Serializable]
        private sealed class CombatTargetConfig
        {
            [SerializeField]
            private EntityIdHolder _entityIdHolder;

            [SerializeField]
            private float _maxHealth = 100f;

            [SerializeField]
            private float _startingHealth = 100f;

            [SerializeField]
            private float _defense = 0f;

            public EntityIdHolder EntityIdHolder => _entityIdHolder;
            public float MaxHealth => _maxHealth;
            public float StartingHealth => _startingHealth;
            public float Defense => _defense;
        }
    }
}
