using UnityEngine;
using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Features.Skill.Domain.ValueObjects;
using SwDreams.Shared.Domain.ValueObjects;

namespace SwDreams.Features.Skill.Adapter.Data
{
    /// <summary>
    /// 혼돈 스킬 데이터. 등급별 수치 파라미터 포함.
    ///
    /// paramsByRarity 는 길이 4 (Common / Rare / Epic / Legendary). 각 슬롯의
    /// (primary, secondary, tertiary) 의미는 chaosEffectType 마다 다름 —
    /// 아래 주석 / ChaosSkillManager 참조.
    ///
    /// 파라미터 매핑 (구현된 6 종):
    ///   GlassCannon    : primary=공격력 배율 (ATK ×),          secondary=HP 비율 (0.5 고정),      tertiary=미사용
    ///   ChainExplosion : primary=폭발 데미지,                  secondary=반경,                    tertiary=미사용 (프레임당 최대 연쇄는 manager SerializeField)
    ///   BerserkMode    : primary=CDR 배율 (발동 시),          secondary=HP 임계 비율 (0.3 고정), tertiary=이속 배율 (Multiplicative)
    ///   AccelEngine    : primary=최대 증폭 (시간 끝 값),      secondary=램프 시간(초),           tertiary=미사용
    ///   Unity          : primary=1명 근접 시 보너스,          secondary=추가 인당 증가,          tertiary=감지 반경 (0 이면 SerializeField 기본값)
    ///   Gambler        : (파라미터 없음 — boolean flag)
    /// 미구현 스킬의 파라미터 매핑은 해당 핸들러 구현 시 결정.
    /// </summary>
    [CreateAssetMenu(fileName = "NewChaosSkill", menuName = "SwDreams/Skill/Chaos")]
    public class ChaosSkillData : SkillData
    {
        [Header("혼돈 파라미터 (등급별)")]
        [Tooltip("길이 4. 인덱스 = Common / Rare / Epic / Legendary. " +
                 "(primary, secondary, tertiary) 의미는 chaosEffectType 별로 다름 (SO 주석 참조).")]
        public EffectParams[] paramsByRarity = new EffectParams[4];

        /// <summary>지정 등급의 파라미터. 범위 밖이면 default.</summary>
        public EffectParams GetParams(Rarity r)
        {
            int idx = (int)r;
            if (paramsByRarity == null || idx < 0 || idx >= paramsByRarity.Length) return default;
            return paramsByRarity[idx];
        }

        private void OnValidate()
        {
            if (paramsByRarity == null || paramsByRarity.Length != 4)
            {
                var resized = new EffectParams[4];
                if (paramsByRarity != null)
                {
                    int n = Mathf.Min(paramsByRarity.Length, 4);
                    for (int i = 0; i < n; i++) resized[i] = paramsByRarity[i];
                }
                paramsByRarity = resized;
            }
        }
    }
}

