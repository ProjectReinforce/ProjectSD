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

        private readonly Dictionary<DomainEntityId, CombatTarget> _targetsById =
            new Dictionary<DomainEntityId, CombatTarget>();

        public void Initialize()
        {
            _targetsById.Clear();

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
                if (_targetsById.ContainsKey(targetId))
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

                _targetsById.Add(targetId, target);
            }
        }

        public bool Exists(DomainEntityId targetId)
        {
            return _targetsById.ContainsKey(targetId);
        }

        public float GetDefense(DomainEntityId targetId)
        {
            if (!_targetsById.TryGetValue(targetId, out var target))
            {
                Debug.LogError($"[CombatTargetAdapter] Target not found: {targetId.Value}", this);
                return 0f;
            }

            return target.Defense;
        }

        public CombatTargetDamageResult ApplyDamage(DomainEntityId targetId, float damage)
        {
            if (!_targetsById.TryGetValue(targetId, out var target))
            {
                Debug.LogError($"[CombatTargetAdapter] Target not found: {targetId.Value}", this);
                return new CombatTargetDamageResult(0f, false);
            }

            var remainingHealth = target.ApplyDamage(damage);
            return new CombatTargetDamageResult(remainingHealth, target.IsDead);
        }

        public bool ResetTarget(DomainEntityId targetId)
        {
            if (!_targetsById.TryGetValue(targetId, out var target))
            {
                Debug.LogError($"[CombatTargetAdapter] Target not found: {targetId.Value}", this);
                return false;
            }

            target.Reset();
            return true;
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
