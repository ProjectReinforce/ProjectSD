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

        [SerializeField]
        private SkillCatalogData _catalogData;

        [SerializeField]
        private SkillLoadoutData _loadoutData;

        private EventBus _eventBus;
        private SkillNetworkEventHandler _networkEventHandler;
        private SkillCatalog _catalog;

        public SkillCatalog Catalog => _catalog;

        public void Initialize(EventBus eventBus, Transform playerTransform)
        {
            _eventBus = eventBus;

            if (_catalogData == null)
            {
                Debug.LogError("[SkillSetup] _catalogData is not assigned in Inspector.", this);
                return;
            }

            if (_loadoutData == null)
            {
                Debug.LogError("[SkillSetup] _loadoutData is not assigned in Inspector.", this);
                return;
            }

            _catalog = new SkillCatalog(_catalogData);

            _barView.Initialize(eventBus);
            _skillCastEffectSpawner.Initialize(eventBus, new SkillEffectAdapter(_catalog));

            _networkEventHandler = new SkillNetworkEventHandler(_eventBus, _networkAdapter);

            var loadoutRepo = new SkillLoadoutRepository(_loadoutData);
            var equipSkillUseCase = new EquipSkillUseCase(_eventBus);
            var skillBar = equipSkillUseCase.BuildFromLoadout(
                loadoutRepo.Load(),
                skillId => _catalog.Get(skillId)
            );

            var cooldownTracker = new CooldownTracker();
            var castSkillUseCase = new CastSkillUseCase(cooldownTracker, _networkAdapter);
            var casterId = Shared.Kernel.DomainEntityId.New();

            if (_slotInputHandler == null)
            {
                Debug.LogError(
                    "[SkillSetup] _slotInputHandler is not assigned in Inspector.",
                    this
                );
                return;
            }

            _slotInputHandler.Initialize(castSkillUseCase, skillBar, casterId);
            _slotInputHandler.SetPlayerTransform(playerTransform);
        }
    }
}
