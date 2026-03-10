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

            ICombatTargetPort targetPort = null; // TODO: Infrastructure 구현체 주입
            _applyDamage = new ApplyDamageUseCase(targetPort, _sceneContext.Publisher);
        }
    }
}
