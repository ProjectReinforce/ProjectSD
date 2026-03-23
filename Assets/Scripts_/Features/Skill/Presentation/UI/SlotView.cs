using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Features.Skill.Presentation
{
    public sealed class SlotView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private Text keyLabel;

        private float _cooldownDuration;
        private float _cooldownEndTime;
        private bool _isCoolingDown;
        private System.Action _onClick;

        public void SetKeyLabel(string label)
        {
            if (keyLabel != null) keyLabel.text = label;
        }

        public void SetSkill(Sprite skillIcon)
        {
            if (icon != null)
            {
                icon.sprite = skillIcon;
                icon.enabled = skillIcon != null;
            }

            ClearCooldown();
        }

        public void ClearSkill()
        {
            if (icon != null)
            {
                icon.sprite = null;
                icon.enabled = false;
            }

            ClearCooldown();
        }

        public void StartCooldown(float duration)
        {
            _cooldownDuration = duration;
            _cooldownEndTime = Time.time + duration;
            _isCoolingDown = true;

            if (cooldownOverlay != null)
            {
                cooldownOverlay.enabled = true;
                cooldownOverlay.fillAmount = 1f;
            }
        }

        private void Update()
        {
            if (!_isCoolingDown) return;

            var remaining = _cooldownEndTime - Time.time;
            if (remaining <= 0f)
            {
                ClearCooldown();
                return;
            }

            if (cooldownOverlay != null)
                cooldownOverlay.fillAmount = remaining / _cooldownDuration;
        }

        public void SetClickHandler(System.Action onClick)
        {
            _onClick = onClick;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
                return;

            _onClick?.Invoke();
        }

        private void ClearCooldown()
        {
            _isCoolingDown = false;
            if (cooldownOverlay != null)
            {
                cooldownOverlay.fillAmount = 0f;
                cooldownOverlay.enabled = false;
            }
        }
    }
}
