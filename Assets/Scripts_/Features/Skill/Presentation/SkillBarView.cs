using Features.Skill.Application.Events;
using Features.Skill.Domain;
using Shared.EventBus;
using Shared.Kernel;
using UnityEngine;

namespace Features.Skill.Presentation
{
    public sealed class SkillBarView : MonoBehaviour
    {
        [SerializeField] private SkillSlotView[] slotViews;
        private static readonly string[] SlotLabels = { "RMB", "Q", "E", "R" };

        private IEventSubscriber _eventBus;
        private SkillBar _skillBar;

        public void Initialize(IEventSubscriber eventBus, SkillBar skillBar)
        {
            _eventBus = eventBus;
            _skillBar = skillBar;

            for (var i = 0; i < slotViews.Length; i++)
            {
                if (slotViews[i] == null) continue;
                slotViews[i].SetKeyLabel(i < SlotLabels.Length ? SlotLabels[i] : string.Empty);
                slotViews[i].ClearSkill();
            }

            _eventBus.Subscribe(this, new System.Action<SkillEquippedEvent>(OnSkillEquipped));
            _eventBus.Subscribe(this, new System.Action<SkillCastedEvent>(OnSkillCasted));
        }

        private void OnDestroy()
        {
            _eventBus?.UnsubscribeAll(this);
        }

        private void OnSkillEquipped(SkillEquippedEvent e)
        {
            if (e.SlotIndex < 0 || e.SlotIndex >= slotViews.Length) return;
            // 아이콘은 프리팹에서 세팅; 여기서는 슬롯 활성화만 표시
            slotViews[e.SlotIndex].SetSkill(null);
            Debug.Log($"[SkillBarView] Slot {e.SlotIndex} equipped: {e.SkillId}");
        }

        private void OnSkillCasted(SkillCastedEvent e)
        {
            for (var i = 0; i < SkillBar.SlotCount; i++)
            {
                var skill = _skillBar.GetSkill(i);
                if (skill == null) continue;
                if (!skill.Id.Equals(e.SkillId)) continue;

                if (i < slotViews.Length)
                    slotViews[i].StartCooldown(e.Spec.Cooldown);

                break;
            }
        }
    }
}
