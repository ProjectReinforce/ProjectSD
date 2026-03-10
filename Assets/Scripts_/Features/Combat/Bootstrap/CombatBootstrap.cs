using Features.Combat.Application;
using Features.Combat.Application.Ports;
using Shared.Context;
using Shared.EventBus;
using UnityEngine;

namespace Features.Combat.Bootstrap
{
    public sealed class CombatBootstrap : MonoBehaviour
    {
        [SerializeField] private SceneContext _sceneContext;

        private ApplyDamageUseCase _applyDamage;
        private IEventSubscriber _subscriber;

        private void Awake()
        {
            _subscriber = _sceneContext.Subscriber;

            throw new System.NotImplementedException("ICombatTargetPort infrastructure 구현체가 필요합니다.");
        }
    }
}
