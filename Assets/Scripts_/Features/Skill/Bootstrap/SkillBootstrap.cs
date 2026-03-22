using Features.Projectile.Bootstrap;
using Features.Skill.Application;
using Features.Skill.Application.Ports;
using Features.Skill.Domain;
using Features.Skill.Presentation;
using Shared.EventBus;
using UnityEngine;
using UnityEngine.Serialization;

namespace Features.Skill.Bootstrap
{
    [DefaultExecutionOrder(-1000)]
    public sealed class SkillBootstrap : MonoBehaviour
    {
        [SerializeField]
        private BarView skillBarView;

        [SerializeField]
        private SlotInputHandler skillInputHandler;

        [FormerlySerializedAs("skillEffectSpawner")]
        [SerializeField]
        private SkillCastEffectSpawner skillCastEffectSpawner;

        [SerializeField]
        private ProjectileSpawner projectileSpawner;

        private EventBus _eventBus;
        private CastSkillUseCase _castSkillUseCase;

        private void Awake()
        {
            if (
                skillBarView == null
                || skillInputHandler == null
                || skillCastEffectSpawner == null
                || projectileSpawner == null
            )
            {
                Debug.LogError("[SkillBootstrap] Missing required components on SkillBarCanvas.");
                return;
            }

            _eventBus = new EventBus();
            var cooldownTracker = new CooldownTracker();
            _castSkillUseCase = new CastSkillUseCase(_eventBus, cooldownTracker);
            var equipSkillUseCase = new EquipSkillUseCase(_eventBus);
            var casterId = Shared.Kernel.DomainEntityId.New();

            var skillBar = new SkillBar();
            equipSkillUseCase.Execute(skillBar, 0, SkillCatalog.Fireball());
            equipSkillUseCase.Execute(skillBar, 1, SkillCatalog.IceLance());
            equipSkillUseCase.Execute(skillBar, 2, SkillCatalog.Blizzard());
            equipSkillUseCase.Execute(skillBar, 3, SkillCatalog.Smite());

            skillBarView.Initialize(_eventBus, skillBar);
            skillInputHandler.Initialize(_castSkillUseCase, skillBar, casterId);
            projectileSpawner.Initialize(_eventBus, _eventBus);
            skillCastEffectSpawner.Initialize(_eventBus);

            Debug.Log("[SkillBootstrap] Skill system ready. Waiting for player connection.");
        }

        public void ConnectLocalPlayer(ISkillNetworkCommandPort networkPort, Transform playerTransform)
        {
            _castSkillUseCase.SetNetwork(networkPort);
            skillInputHandler.SetPlayerTransform(playerTransform);
            Debug.Log("[SkillBootstrap] Local player connected.");
        }

        public void RegisterRemotePlayer(ISkillNetworkCallbackPort callbackPort)
        {
            var _ = new SkillNetworkEventHandler(_eventBus, callbackPort);
            Debug.Log("[SkillBootstrap] Remote player registered.");
        }
    }
}
