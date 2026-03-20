using Features.Combat.Application;
using Features.Combat.Application.Ports;
using Shared.EventBus;
using UnityEngine;

namespace Features.Combat.Bootstrap
{
    public sealed class CombatBootstrap : MonoBehaviour
    {
        private readonly EventBus _eventBus = new EventBus();

        private ApplyDamageUseCase _applyDamage;

        private void Awake()
        {
            throw new System.NotImplementedException("ICombatTargetPort infrastructure 구현체가 필요합니다.");
        }
    }
}
