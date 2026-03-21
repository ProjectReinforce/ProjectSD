using System.Collections;
using Features.Skill.Application;
using UnityEngine;
using DomainSkill = Features.Skill.Domain.Skill;

namespace Features.Skill.Bootstrap
{
    public sealed class SkillAutoCaster : MonoBehaviour
    {
        [SerializeField] private float castIntervalSeconds = 1.5f;

        private CastSkillUseCase _castSkillUseCase;
        private DomainSkill[] _skills;
        private Shared.Kernel.DomainEntityId _casterId;
        private int _nextSkillIndex;
        private Coroutine _autoCastRoutine;

        public void Initialize(
            CastSkillUseCase castSkillUseCase,
            DomainSkill[] skills,
            Shared.Kernel.DomainEntityId casterId)
        {
            _castSkillUseCase = castSkillUseCase;
            _skills = skills;
            _casterId = casterId;
        }

        private void Start()
        {
            CastNextSkill();
            _autoCastRoutine = StartCoroutine(AutoCastLoop());
        }

        private void OnDestroy()
        {
            if (_autoCastRoutine != null)
            {
                StopCoroutine(_autoCastRoutine);
                _autoCastRoutine = null;
            }
        }

        private IEnumerator AutoCastLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(castIntervalSeconds);
                CastNextSkill();
            }
        }

        private void CastNextSkill()
        {
            if (_skills == null || _skills.Length == 0)
            {
                Debug.LogWarning("[SkillTest] No skills configured for the test rig.");
                return;
            }

            var skill = _skills[_nextSkillIndex];
            _nextSkillIndex = (_nextSkillIndex + 1) % _skills.Length;

            var result = _castSkillUseCase.Execute(skill, _casterId, Time.time);
            if (result.IsFailure)
            {
                Debug.LogWarning($"[SkillTest] FAILED: {result.Error}");
            }
        }
    }
}
