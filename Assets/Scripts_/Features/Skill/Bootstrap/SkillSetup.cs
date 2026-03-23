using Features.Skill.Application;
using Features.Skill.Domain;
using Features.Skill.Infrastructure;
using Features.Skill.Presentation;
using Shared.EventBus;
using Shared.Kernel;
using System.Collections.Generic;
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
        private SkillCatalog _catalog;
        private EquipSkillUseCase _equipSkillUseCase;
        private SkillBar _skillBar;
        private SkillRotator _skillRotator;

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
            _skillRotator = new SkillRotator(CollectCatalogSkillIds());

            _barView.Initialize(eventBus, new SkillIconAdapter(_catalog));
            _skillCastEffectSpawner.Initialize(eventBus, new SkillEffectAdapter(_catalog));

            new SkillNetworkEventHandler(_eventBus, _networkAdapter);

            var cooldownTracker = new CooldownTracker();

            var loadoutRepo = new SkillLoadoutRepository(_loadoutData);
            _equipSkillUseCase = new EquipSkillUseCase(_eventBus, cooldownTracker);
            _skillBar = _equipSkillUseCase.BuildFromLoadout(
                loadoutRepo.Load(),
                skillId => _catalog.Get(skillId)
            );
            _barView.SetSlotClickHandler(HandleSlotClicked);

            var castSkillUseCase = new CastSkillUseCase(cooldownTracker, _networkAdapter);
            var casterId = DomainEntityId.New();

            if (_slotInputHandler == null)
            {
                Debug.LogError(
                    "[SkillSetup] _slotInputHandler is not assigned in Inspector.",
                    this
                );
                return;
            }

            _slotInputHandler.Initialize(castSkillUseCase, _skillBar, casterId);
            _slotInputHandler.SetPlayerTransform(playerTransform);
        }

        public Result SwapSkill(int slotIndex, string skillId)
        {
            var skill = _catalog.Get(skillId);
            if (skill == null)
                return Result.Failure($"Skill not found: {skillId}");

            return _equipSkillUseCase.Execute(_skillBar, slotIndex, skill);
        }

        private List<string> CollectCatalogSkillIds()
        {
            var ids = new List<string>();
            if (_catalog?.AllSkills == null)
                return ids;

            foreach (var skillData in _catalog.AllSkills)
            {
                if (skillData == null || string.IsNullOrWhiteSpace(skillData.SkillId))
                    continue;
                if (ids.Contains(skillData.SkillId))
                    continue;
                ids.Add(skillData.SkillId);
            }

            return ids;
        }

        private void HandleSlotClicked(int slotIndex)
        {
            if (_skillRotator.Count == 0)
            {
                Debug.LogWarning("[SkillSetup] No skills available for runtime swap.", this);
                return;
            }

            var nextSkillId = _skillRotator.GetNext(_skillBar, slotIndex);
            if (string.IsNullOrEmpty(nextSkillId))
                return;

            var result = SwapSkill(slotIndex, nextSkillId);
            if (result.IsFailure)
                Debug.LogWarning($"[SkillSetup] Runtime swap failed: {result.Error}", this);
        }
    }
}
