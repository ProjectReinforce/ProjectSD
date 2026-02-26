using System;
using UnityEngine;

namespace SwDreams.Adapter.Skill
{
    /// <summary>
    /// 플레이어 스탯 관리. Base + Bonus = Final 구조.
    ///
    /// Base: 캐릭터 기본 수치 (인스펙터 설정).
    /// Bonus: 패시브 스킬에 의한 보정값.
    /// Final: 실제 게임에서 사용되는 최종 수치.
    ///
    /// 패시브 변경 시 RecalculateAll()로 Bonus를 전체 재계산.
    /// "덮어쓰기" 방식이라 레벨업 시 중복 적용 버그 없음.
    ///
    /// PlayerStub(또는 Player)에 부착.
    /// SkillManager.OnPassiveChanged 이벤트에 RecalculateAll 연결.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        // ===== Base 스탯 (인스펙터 설정) =====
        [Header("Base Stats")]
        [SerializeField] private float baseAttackMultiplier = 1f;
        [SerializeField] private float baseMoveSpeed = 5f;
        [SerializeField] private int baseMaxHP = 100;
        [SerializeField] private float baseProjectileSpeed = 0f;   // 0이면 SkillData 기본값 사용
        [SerializeField] private int baseProjectileCount = 0;       // 0이면 SkillData 기본값 사용
        [SerializeField] private float baseSkillRange = 0f;         // 0이면 SkillData 기본값 사용
        [SerializeField] private float baseCooldownReduction = 0f;  // 0~1 비율
        [SerializeField] private float baseKnockback = 1f;
        [SerializeField] private float baseCritDamage = 1.5f;       // 치명타 데미지 배율
        [SerializeField] private float baseExpMultiplier = 1f;
        [SerializeField] private float baseDefenseMultiplier = 1f;
        [SerializeField] private float baseHealMultiplier = 1f;
        [SerializeField] private float baseSkillDuration = 0f;      // 추가 지속시간

        // ===== Bonus (패시브에 의한 보정) =====
        // RecalculateAll()에서만 수정됨
        private float bonusAttackMultiplier;
        private float bonusMoveSpeed;
        private int bonusMaxHP;
        private float bonusProjectileSpeed;
        private int bonusProjectileCount;
        private float bonusSkillRange;
        private float bonusCooldownReduction;
        private float bonusKnockback;
        private float bonusCritDamage;
        private float bonusExpMultiplier;
        private float bonusDefenseMultiplier;
        private float bonusHealMultiplier;
        private float bonusSkillDuration;

        // ===== Final (외부에서 읽기 전용) =====
        public float AttackMultiplier => baseAttackMultiplier + bonusAttackMultiplier;
        public float MoveSpeed => baseMoveSpeed + bonusMoveSpeed;
        public int MaxHP => baseMaxHP + bonusMaxHP;
        public float ProjectileSpeedBonus => baseProjectileSpeed + bonusProjectileSpeed;
        public int ProjectileCountBonus => baseProjectileCount + bonusProjectileCount;
        public float SkillRangeBonus => baseSkillRange + bonusSkillRange;
        public float CooldownReduction => Mathf.Clamp01(baseCooldownReduction + bonusCooldownReduction);
        public float KnockbackMultiplier => baseKnockback + bonusKnockback;
        public float CritDamageMultiplier => baseCritDamage + bonusCritDamage;
        public float ExpMultiplier => baseExpMultiplier + bonusExpMultiplier;
        public float DefenseMultiplier => baseDefenseMultiplier + bonusDefenseMultiplier;
        public float HealMultiplier => baseHealMultiplier + bonusHealMultiplier;
        public float SkillDurationBonus => baseSkillDuration + bonusSkillDuration;

        // ===== 이벤트 =====
        /// <summary>스탯 재계산 완료 시 발생. UI 갱신, 이동속도 적용 등.</summary>
        public event Action OnStatsChanged;

        // ===== SkillManager 참조 =====
        private SkillManager skillManager;

        private void Awake()
        {
            skillManager = GetComponentInChildren<SkillManager>();
        }

        private void OnEnable()
        {
            if (skillManager != null)
                skillManager.OnPassiveChanged += RecalculateAll;
        }

        private void OnDisable()
        {
            if (skillManager != null)
                skillManager.OnPassiveChanged -= RecalculateAll;
        }

        /// <summary>
        /// 모든 Bonus를 0으로 초기화한 뒤,
        /// 보유 중인 패시브 스킬을 전부 순회하며 Bonus를 새로 계산.
        /// "전체 재계산" 방식으로 중복 적용 버그 방지.
        /// </summary>
        public void RecalculateAll()
        {
            // 1) Bonus 초기화
            bonusAttackMultiplier = 0f;
            bonusMoveSpeed = 0f;
            bonusMaxHP = 0;
            bonusProjectileSpeed = 0f;
            bonusProjectileCount = 0;
            bonusSkillRange = 0f;
            bonusCooldownReduction = 0f;
            bonusKnockback = 0f;
            bonusCritDamage = 0f;
            bonusExpMultiplier = 0f;
            bonusDefenseMultiplier = 0f;
            bonusHealMultiplier = 0f;
            bonusSkillDuration = 0f;

            if (skillManager == null) return;

            // 2) 패시브 스킬 순회
            var passives = skillManager.GetSkillsByType(Data.SkillType.Passive);
            for (int i = 0; i < passives.Count; i++)
            {
                ApplyPassiveBonus(passives[i]);
            }

            // 3) 이벤트 발행
            OnStatsChanged?.Invoke();

            Debug.Log($"[PlayerStats] 재계산 완료 — ATK:{AttackMultiplier:F2} SPD:{MoveSpeed:F1} HP:{MaxHP} CDR:{CooldownReduction:P0}");
        }

        /// <summary>
        /// 개별 패시브 스킬의 Bonus 적용.
        /// 패시브 종류는 SkillData.skillId로 구분.
        ///
        /// TODO: 패시브별 레벨 스케일링 테이블이 필요.
        /// 지금은 skill_design_v3 기반으로 레벨당 고정 증가량 사용.
        /// Phase 5에서 PassiveData SO로 분리 가능.
        /// </summary>
        private void ApplyPassiveBonus(Skill passive)
        {
            int level = passive.Level;
            Data.SkillData data = passive.Data;

            // SkillData.skillId 기반 분기
            // skill_design_v3.docx 패시브 13종:
            //  1: 투사체 속도 증가
            //  2: 투사체 개수 증가
            //  3: 스킬 범위 증가
            //  4: 스킬 유지 시간 증가
            //  5: 공격력 증가
            //  6: 넉백 거리 증가
            //  7: 체력 회복량 증가
            //  8: 치명타 데미지 증가
            //  9: 스킬 쿨타임 감소
            // 10: 최대 체력 증가
            // 11: 이동속도 증가
            // 12: 방어력 증가
            // 13: 경험치 획득량 증가
            //
            // 레벨당 증가량은 Phase 7 밸런싱에서 조정.
            // 지금은 선형 증가 (레벨 × 기본 단위).

            switch (data.skillId)
            {
                case 101: // 투사체 속도 증가
                    bonusProjectileSpeed += level * 1.5f;
                    break;

                case 102: // 투사체 개수 증가
                    // Lv.1 = +1, Lv.4 이상 = +2 (계단식)
                    bonusProjectileCount += (level >= 4) ? 2 : 1;
                    break;

                case 103: // 스킬 범위 증가
                    bonusSkillRange += level * 0.3f;
                    break;

                case 104: // 스킬 유지 시간 증가
                    bonusSkillDuration += level * 0.5f;
                    break;

                case 105: // 공격력 증가
                    bonusAttackMultiplier += level * 0.1f; // Lv.1 = +10%, Lv.7 = +70%
                    break;

                case 106: // 넉백 거리 증가
                    bonusKnockback += level * 0.15f;
                    break;

                case 107: // 체력 회복량 증가
                    bonusHealMultiplier += level * 0.15f;
                    break;

                case 108: // 치명타 데미지 증가
                    bonusCritDamage += level * 0.2f;
                    break;

                case 109: // 스킬 쿨타임 감소
                    bonusCooldownReduction += level * 0.04f; // Lv.7 = 28% 감소
                    break;

                case 110: // 최대 체력 증가
                    bonusMaxHP += level * 15;
                    break;

                case 111: // 이동속도 증가
                    bonusMoveSpeed += level * 0.4f;
                    break;

                case 112: // 방어력 증가
                    bonusDefenseMultiplier += level * 0.05f;
                    break;

                case 113: // 경험치 획득량 증가
                    bonusExpMultiplier += level * 0.1f;
                    break;

                default:
                    // 미등록 패시브 ID → 경고만 (크래시 방지)
                    Debug.LogWarning($"[PlayerStats] 미등록 패시브 ID: {data.skillId} ({data.skillName})");
                    break;
            }
        }

        // ===== 외부 유틸리티 =====

        /// <summary>
        /// 실제 쿨다운 계산. Skill.CurrentCooldown에 CDR 적용.
        /// Skill.Fire() 시 이 값을 사용해야 함.
        /// </summary>
        public float GetEffectiveCooldown(float baseCooldown)
        {
            return baseCooldown * (1f - CooldownReduction);
        }

        /// <summary>
        /// 실제 투사체 개수. SkillData.projectileCount + 보너스.
        /// ProjectileEffect.Execute() 시 이 값 사용.
        /// </summary>
        public int GetEffectiveProjectileCount(int baseCount)
        {
            return baseCount + ProjectileCountBonus;
        }

        /// <summary>
        /// 실제 투사체 속도. SkillData.projectileSpeed + 보너스.
        /// </summary>
        public float GetEffectiveProjectileSpeed(float baseSpeed)
        {
            return baseSpeed + ProjectileSpeedBonus;
        }
    }
}