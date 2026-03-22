using Features.Skill.Application.Events;
using Shared.EventBus;
using UnityEngine;

namespace Features.Skill.Presentation
{
    public sealed class BarView : MonoBehaviour
    {
        [SerializeField]
        private SlotView[] slotViews;
        private static readonly string[] SlotLabels = { "RMB", "Q", "E", "R" };

        private IEventSubscriber _eventBus;

        public void Initialize(IEventSubscriber eventBus)
        {
            _eventBus = eventBus;

            for (var i = 0; i < slotViews.Length; i++)
            {
                if (slotViews[i] == null)
                    continue;
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
            if (e.SlotIndex < 0 || e.SlotIndex >= slotViews.Length)
                return;
            slotViews[e.SlotIndex].SetSkill(null);
            Debug.Log($"[BarView] Slot {e.SlotIndex} equipped: {e.SkillId}");
        }

        private void OnSkillCasted(SkillCastedEvent e)
        {
            if (e.SlotIndex < 0 || e.SlotIndex >= slotViews.Length)
                return;
            slotViews[e.SlotIndex].StartCooldown(e.Spec.Cooldown);
        }
    }
}
