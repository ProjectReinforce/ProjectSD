using Features.Skill.Application.Events;
using Features.Skill.Application.Ports;
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

        /// <summary>로드아웃과 카탈로그로부터 SkillBar를 조립한다.</summary>
        public SkillBar BuildFromLoadout(SkillLoadout loadout, System.Func<string, DomainSkill> skillResolver)
        {
            var bar = new SkillBar();
            var ids = loadout.SlotSkillIds;

            for (var i = 0; i < ids.Length && i < SkillBar.SlotCount; i++)
            {
                if (string.IsNullOrEmpty(ids[i])) continue;

                var skill = skillResolver(ids[i]);
                if (skill == null) continue;

                Execute(bar, i, skill);
            }

            return bar;
        }
    }
}
