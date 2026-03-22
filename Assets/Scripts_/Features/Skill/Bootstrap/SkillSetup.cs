using Features.Skill.Application;
using Features.Skill.Domain;
using Features.Skill.Infrastructure;
using Features.Skill.Presentation;
using Shared.EventBus;
using UnityEngine;

namespace Features.Skill.Bootstrap
{
    public sealed class SkillSetup : MonoBehaviour
    {
        [SerializeField]
        private SlotInputHandler _slotInputHandler;

        [SerializeField]
        private SkillCastEffectSpawner _skillCastEffectSpawner;

        [SerializeField]
        private BarView _barView;

        [SerializeField]
        private SkillNetworkAdapter _networkAdapter;
        private EventBus _eventBus;

        public void Initialize(EventBus eventBus, Transform playerTransform)
        {
            _eventBus = eventBus;
            _barView.Initialize(eventBus);
            _skillCastEffectSpawner.Initialize(eventBus);

            var _ = new SkillNetworkEventHandler(_eventBus, _networkAdapter);

            var skillBar = new SkillBar();
            var equipSkillUseCase = new EquipSkillUseCase(_eventBus);
            equipSkillUseCase.Execute(skillBar, 0, SkillCatalog.Fireball());
            equipSkillUseCase.Execute(skillBar, 1, SkillCatalog.IceLance());
            equipSkillUseCase.Execute(skillBar, 2, SkillCatalog.Blizzard());
            equipSkillUseCase.Execute(skillBar, 3, SkillCatalog.Smite());

            var cooldownTracker = new CooldownTracker();
            var castSkillUseCase = new CastSkillUseCase(cooldownTracker, _networkAdapter);
            var casterId = Shared.Kernel.DomainEntityId.New();

            if (_slotInputHandler == null)
            {
                Debug.LogError(
                    $"[SkillSetup] _slotInputHandler is not assigned in Inspector.",
                    this
                );
                return;
            }

            _slotInputHandler.Initialize(castSkillUseCase, skillBar, casterId);
            _slotInputHandler.SetPlayerTransform(playerTransform);
        }
    }
}
