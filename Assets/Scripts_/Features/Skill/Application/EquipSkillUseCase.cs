using Features.Skill.Application.Events;
using Features.Skill.Domain;
using Shared.EventBus;
using Shared.Kernel;

using DomainSkill = Features.Skill.Domain.Skill;

namespace Features.Skill.Application
{
    public sealed class EquipSkillUseCase
    {
        private readonly IEventPublisher _eventBus;

        public EquipSkillUseCase(IEventPublisher eventBus)
        {
            _eventBus = eventBus;
        }

        public Result Execute(SkillBar bar, int slotIndex, DomainSkill skill)
        {
            if (slotIndex < 0 || slotIndex >= SkillBar.SlotCount)
                return Result.Failure("Invalid slot index.");

            bar.Equip(slotIndex, skill);
            _eventBus.Publish(new SkillEquippedEvent(slotIndex, skill.Id, skill.Spec));
            return Result.Success();
        }
    }
}
