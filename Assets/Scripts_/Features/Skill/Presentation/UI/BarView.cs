using Features.Skill.Application.Events;
using Features.Skill.Application.Ports;
using Shared.EventBus;
using System;
using UnityEngine;

namespace Features.Skill.Presentation
{
    public sealed class BarView : MonoBehaviour
    {
        [SerializeField]
        private SlotView[] slotViews;
        private static readonly string[] SlotLabels = { "RMB", "Q", "E", "R" };

        private IEventSubscriber _eventBus;
        private ISkillIconPort _iconPort;
        private Action<int> _onSlotClicked;

        public void Initialize(IEventSubscriber eventBus, ISkillIconPort iconPort)
        {
            _eventBus = eventBus;
            _iconPort = iconPort;

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

        public void SetSlotClickHandler(Action<int> onSlotClicked)
        {
            _onSlotClicked = onSlotClicked;

            for (var i = 0; i < slotViews.Length; i++)
            {
                var slotIndex = i;
                var slotView = slotViews[i];
                if (slotView == null)
                    continue;

                slotView.SetClickHandler(() => _onSlotClicked?.Invoke(slotIndex));
            }
        }

        private void OnDestroy()
        {
            _eventBus?.UnsubscribeAll(this);
        }

        private void OnSkillEquipped(SkillEquippedEvent e)
        {
            if (e.SlotIndex < 0 || e.SlotIndex >= slotViews.Length)
                return;
            var icon = _iconPort?.GetIcon(e.SkillId.Value);
            slotViews[e.SlotIndex].SetSkill(icon);
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
