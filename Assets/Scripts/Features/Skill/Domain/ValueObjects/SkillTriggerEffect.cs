using System;
using SwDreams.Features.Skill.Domain.ValueObjects;
using UnityEngine;

namespace SwDreams.Features.Skill.Domain.ValueObjects
{
    /// <summary>
    /// 효과 파라미터. EffectActionType에 따라 각 필드의 의미가 달라짐.
    /// 각 ActionType별 매핑은 EffectActionType 주석 참조.
    ///
    /// [Phase 7 리팩토링] Step 3-1
    /// </summary>
    [Serializable]
    public struct EffectParams
    {
        [Tooltip("주요 수치. 용도는 EffectActionType에 따라 다름.")]
        public float primary;

        [Tooltip("보조 수치. 용도는 EffectActionType에 따라 다름.")]
        public float secondary;

        [Tooltip("추가 수치. 용도는 EffectActionType에 따라 다름.")]
        public float tertiary;

        public EffectParams(float primary, float secondary = 0f, float tertiary = 0f)
        {
            this.primary = primary;
            this.secondary = secondary;
            this.tertiary = tertiary;
        }

        public override string ToString()
        {
            if (tertiary != 0f) return $"({primary}, {secondary}, {tertiary})";
            if (secondary != 0f) return $"({primary}, {secondary})";
            return $"({primary})";
        }
    }

    /// <summary>
    /// 트리거 + 효과 조합 한 세트. SO에서 기본 효과를 정의할 때 사용.
    /// Serializable이므로 인스펙터에서 편집 가능.
    ///
    /// 예: { trigger: OnHit, action: Explode, parameters: (1.5, 1.0, 0) }
    ///     → 적중 시 반경 1.5, 데미지 100%로 폭발
    /// </summary>
    [Serializable]
    public struct SkillTriggerEffect
    {
        public TriggerType trigger;
        public EffectActionType action;
        public EffectParams parameters;

        public SkillTriggerEffect(TriggerType trigger, EffectActionType action, EffectParams parameters)
        {
            this.trigger = trigger;
            this.action = action;
            this.parameters = parameters;
        }

        public override string ToString()
        {
            return $"{trigger} → {action} {parameters}";
        }
    }

    /// <summary>
    /// 런타임에 동적 추가되는 트리거 효과. source로 추가/제거 관리.
    /// 정수/무기/혼돈 등 외부 시스템에서 스킬에 효과를 부여할 때 사용.
    ///
    /// source 명명 규칙:
    ///   "essence_{이름}"  — 정수 속성
    ///   "weapon_{이름}"   — 무기 부가효과
    ///   "chaos_{이름}"    — 혼돈 스킬
    ///   "buff_{이름}"     — 일시 버프
    /// </summary>
    public struct RuntimeTriggerEffect
    {
        public string source;
        public SkillTriggerEffect effect;

        public RuntimeTriggerEffect(string source, SkillTriggerEffect effect)
        {
            this.source = source;
            this.effect = effect;
        }

        public override string ToString()
        {
            return $"[{source}] {effect}";
        }
    }
}
