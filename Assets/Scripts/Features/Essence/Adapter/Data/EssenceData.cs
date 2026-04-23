using UnityEngine;
using SwDreams.Features.Essence.Domain;
using SwDreams.Features.Skill.Domain.ValueObjects;

namespace SwDreams.Features.Essence.Adapter.Data
{
    /// <summary>
    /// 속성 정수 데이터 SO. 속성별 시각/효과를 정의한다.
    ///
    /// 장착 시 <see cref="injectedEffects"/> 전체가
    /// <c>SkillTriggerSystem.AddRuntimeEffect("essence_{type}", ...)</c> 로 주입.
    /// 해제 시 같은 prefix 로 일괄 제거.
    /// </summary>
    [CreateAssetMenu(fileName = "EssenceData", menuName = "SwDreams/Data/EssenceData")]
    public class EssenceData : ScriptableObject
    {
        [Header("식별")]
        public EssenceType type;
        public string displayName;

        [Header("시각")]
        public Sprite icon;
        [Tooltip("월드 드랍 시 SpriteRenderer 색 틴트 (HUD 아이콘도 이 색 사용 가능).")]
        public Color iconColor = Color.white;

        [Header("주입 효과 (1스택 — 장착 시 모든 스킬 SkillTriggerSystem 에 추가)")]
        [Tooltip("1개 장착 시 각 슬롯이 이 효과를 독립 주입.\n" +
                 "예) 불: OnHit → ApplyDoT (primary=4 틱당, secondary=3초 지속, tertiary=0.5초 간격)")]
        public SkillTriggerEffect[] injectedEffects;

        [Header("주입 효과 (2스택 시너지 — 비우면 단순 합산)")]
        [Tooltip("같은 속성 2개 장착 시 슬롯 0 의 1스택 효과를 이 배열로 교체. 슬롯 1 은 주입 안 함.\n" +
                 "총 효과 = 이 배열 1회분. 비워두면 1스택 × 2 독립 발동(합산).\n" +
                 "예) 불 시너지: 틱당 4 → 틱당 10 (도트 데미지 2.5배)")]
        public SkillTriggerEffect[] injectedEffectsStack2;
    }
}
