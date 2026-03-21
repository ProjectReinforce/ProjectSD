using Features.Skill.Application;
using Features.Skill.Domain;
using Shared.Kernel;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Features.Skill.Presentation
{
    [RequireComponent(typeof(PlayerInput))]
    public sealed class SkillInputHandler : MonoBehaviour
    {
        private CastSkillUseCase _castSkillUseCase;
        private SkillBar _skillBar;
        private DomainEntityId _casterId;

        private readonly System.Action<InputAction.CallbackContext>[] _callbacks =
            new System.Action<InputAction.CallbackContext>[SkillBar.SlotCount];

        private InputAction[] _slotActions;

        public void Initialize(CastSkillUseCase castSkillUseCase, SkillBar skillBar, DomainEntityId casterId)
        {
            _castSkillUseCase = castSkillUseCase;
            _skillBar = skillBar;
            _casterId = casterId;

            var playerInput = GetComponent<PlayerInput>();
            _slotActions = new[]
            {
                playerInput.actions["SkillSlot0"],
                playerInput.actions["SkillSlot1"],
                playerInput.actions["SkillSlot2"],
                playerInput.actions["SkillSlot3"]
            };

            for (var i = 0; i < SkillBar.SlotCount; i++)
            {
                var index = i;
                _callbacks[i] = _ => CastSlot(index);
                _slotActions[i].performed += _callbacks[i];
            }
        }

        private void OnDestroy()
        {
            if (_slotActions == null) return;
            for (var i = 0; i < SkillBar.SlotCount; i++)
            {
                if (_slotActions[i] != null)
                    _slotActions[i].performed -= _callbacks[i];
            }
        }

        private void CastSlot(int slotIndex)
        {
            var skill = _skillBar.GetSkill(slotIndex);
            if (skill == null)
            {
                Debug.LogWarning($"[SkillInput] Slot {slotIndex} is empty.");
                return;
            }

            var result = _castSkillUseCase.Execute(skill, _casterId, Time.time);
            if (result.IsFailure)
            {
                Debug.LogWarning($"[SkillInput] Slot {slotIndex} FAILED: {result.Error}");
            }
        }
    }
}
