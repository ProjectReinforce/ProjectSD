using System;
using UnityEngine;
using SwDreams.Data;

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
        /// 보유 패시브 전체 순회 → Bonus 재계산.
        /// SkillManager.OnPassiveChanged 이벤트에서 호출.
        /// </summary>
        public void RecalculateAll()
        {
            // Bonus 초기화
            bonusAttackMultiplier = 0f;
            bonusMoveSpeed = 0f;
            bonusMaxHP = 0;
            bonusProjectileSpeed = 0f;
            bonusProjectileCount = 0;
            bonusSkillRange = 0f;
            bonusSkillDuration = 0f;
            bonusKnockback = 0f;
            bonusHealMultiplier = 0f;
            bonusCritDamage = 0f;
            bonusCooldownReduction = 0f;
            bonusDefenseMultiplier = 0f;
            bonusExpMultiplier = 0f;

            // SkillManager에서 패시브 스킬 목록 가져오기
            var skillManager = GetComponentInChildren<SkillManager>();
            if (skillManager == null) return;

            var passives = skillManager.GetSkillsByType(SkillType.Passive);
            foreach (var skill in passives)
            {
                if (skill == null || skill.Data == null) continue;
                ApplyPassiveBonus(skill.Data, skill.Level);
            }

            OnStatsChanged?.Invoke();
            Debug.Log($"[PlayerStats] 재계산 완료 — ATK:{AttackMultiplier:F2}, " +
                    $"SPD:{MoveSpeed:F1}, ProjSpd:{ProjectileSpeedBonus:F1}");
        }

        /// <summary>
        /// 개별 패시브 보너스 적용. SkillData의 bonusType + bonusPerLevel 사용.
        /// </summary>
        private void ApplyPassiveBonus(SkillData data, int level)
        {
            float bonus = data.bonusPerLevel * level;

            switch (data.bonusType)
            {
                case PassiveBonusType.ProjectileSpeed:
                    bonusProjectileSpeed += bonus;
                    break;
                case PassiveBonusType.ProjectileCount:
                    // 정수로 변환 (소수점은 버림)
                    bonusProjectileCount += Mathf.FloorToInt(bonus);
                    break;
                case PassiveBonusType.SkillRange:
                    bonusSkillRange += bonus;
                    break;
                case PassiveBonusType.SkillDuration:
                    bonusSkillDuration += bonus;
                    break;
                case PassiveBonusType.AttackMultiplier:
                    bonusAttackMultiplier += bonus;
                    break;
                case PassiveBonusType.Knockback:
                    bonusKnockback += bonus;
                    break;
                case PassiveBonusType.HealingMultiplier:
                    bonusHealMultiplier += bonus;
                    break;
                case PassiveBonusType.CritDamage:
                    bonusCritDamage += bonus;
                    break;
                case PassiveBonusType.CooldownReduction:
                    bonusCooldownReduction += bonus;
                    break;
                case PassiveBonusType.MaxHP:
                    bonusMaxHP += Mathf.FloorToInt(bonus);
                    break;
                case PassiveBonusType.MoveSpeed:
                    bonusMoveSpeed += bonus;
                    break;
                case PassiveBonusType.Defense:
                    bonusDefenseMultiplier += bonus;
                    break;
                case PassiveBonusType.ExpMultiplier:
                    bonusExpMultiplier += bonus;
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