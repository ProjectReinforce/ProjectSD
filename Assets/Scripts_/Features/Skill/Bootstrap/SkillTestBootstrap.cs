using Features.Skill.Application;
using Features.Skill.Domain;
using Features.Skill.Presentation;
using Shared.EventBus;
using Shared.Time;
using UnityEngine;

namespace Features.Skill.Bootstrap
{
    [DefaultExecutionOrder(-1000)]
    public sealed class SkillTestBootstrap : MonoBehaviour
    {
        [SerializeField] private SkillBarView skillBarView;
        [SerializeField] private SkillInputHandler skillInputHandler;

        private void Awake()
        {
            var eventBus = new EventBus();
            var clock = new ClockAdapter();
            var cooldownTracker = new CooldownTracker();
            var castSkillUseCase = new CastSkillUseCase(eventBus, cooldownTracker);
            var equipSkillUseCase = new EquipSkillUseCase(eventBus);
            var casterId = Shared.Kernel.DomainEntityId.New();

            var skillBar = new SkillBar();
            equipSkillUseCase.Execute(skillBar, 0, SkillCatalog.Fireball());
            equipSkillUseCase.Execute(skillBar, 1, SkillCatalog.IceLance());
            equipSkillUseCase.Execute(skillBar, 2, SkillCatalog.Blizzard());
            equipSkillUseCase.Execute(skillBar, 3, SkillCatalog.Smite());

            var rigView = gameObject.AddComponent<SkillTestRigView>();
            rigView.Initialize(eventBus, clock);

            if (skillBarView != null)
                skillBarView.Initialize(eventBus, skillBar);

            if (skillInputHandler != null)
                skillInputHandler.Initialize(castSkillUseCase, skillBar, casterId);

            Debug.Log("[SkillTest] Test rig ready. Press RMB/Q/E/R to cast skills.");
        }
    }
}
