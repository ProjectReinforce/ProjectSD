using System;
using SwDreams.Features.Character.Domain.ValueObjects;
using SwDreams.Features.Character.Adapter.Data;
using SwDreams.Features.Character.Adapter;
using SwDreams.Features.Skill.Adapter;
using SwDreams.Features.Skill.Adapter.Data;
using UnityEngine;
using SwDreams.Shared.Data;
using SwDreams.Shared.Domain.Interfaces;

namespace SwDreams.Features.Character.Adapter
{
    /// <summary>
    /// 플레이어 스탯 관리. StatModifierCollection 기반.
    ///
    /// 구조: Base (인스펙터/CharacterData) + Modifier (패시브/혼돈/진화/무기 등) = Final
    /// 계산: (Base + ΣAdd) × (1 + ΣPercentBonus) × ΠMultiplicative, 이후 Clamp 적용
    ///
    /// 외부에서 modifier 등록/해제 → Recalculate() 호출 → OnStatsChanged 이벤트.
    ///
    /// [Phase 7 리팩토링] Step 1-2: 내부 저장소를 StatModifierCollection으로 전환.
    /// - 기존 bonusXxx 필드 제거
    /// - RecalculateAll()은 하위 호환을 위해 유지 (패시브 순회 → modifier 재등록)
    /// - ChaosSkillManager 헬퍼는 Step 1-4에서 modifier로 통합 예정
    /// </summary>
    public class PlayerStats : MonoBehaviour, IPlayerStatsMutator
    {
        // ===== Base 스탯 (인스펙터 설정 / CharacterData로 덮어쓰기) =====
        [Header("Base Stats")]
        [SerializeField] private float baseAttackMultiplier = 1f;
        [SerializeField] private float baseMoveSpeed = 0.8f;
        [SerializeField] private int baseMaxHP = 100;
        [SerializeField] private float baseProjectileSpeed = 0f;
        [SerializeField] private int baseProjectileCount = 0;
        [SerializeField] private float baseSkillRange = 0f;
        [SerializeField] private float baseCooldownReduction = 0f;
        [SerializeField] private float baseKnockback = 1f;
        [SerializeField] private float baseCritDamage = 1.5f;
        [Range(0f, 1f)]
        [SerializeField] private float baseCritChance = 0.05f;
        [SerializeField] private float baseExpMultiplier = 1f;
        [SerializeField] private float baseDefenseMultiplier = 1f;
        [SerializeField] private float baseHealMultiplier = 1f;
        [SerializeField] private float baseSkillDuration = 0f;
        [Tooltip("체력 자연회복 (HP/초). HealMultiplier 영향 안 받음.")]
        [SerializeField] private float baseHpRegen = 0f;
        [Tooltip("피격 후 무적 시간 (초).")]
        [SerializeField] private float baseIFrameDuration = 0.4f;

        // ===== Modifier 컬렉션 (패시브/혼돈/진화/무기/정수 등 모든 보정값) =====
        private readonly StatModifierCollection modifiers = new StatModifierCollection();

        // 재진입 방지
        private bool isRecalculating = false;

        // ===== Final 스탯 (외부 읽기 전용) =====

        /// <summary>
        /// 공격력의 "대표 배율 값". 데미지 계산 경로는 <see cref="ApplyAttackTo"/> 사용 — 새 공식은 skillBase 에 의존하므로
        /// 단일 배율로 환산되지 않음. 이 프로퍼티는 디버그/HUD 표시, 조건부 로직(예: BerserkMode 임계값) 전용.
        /// 값 = `(base + ΣAdd) × (1+ΣPercentBonus) × ΠMultiplicative` (참고용 스냅샷).
        /// </summary>
        public float AttackMultiplier =>
            ClampStat(StatType.AttackMultiplier,
                modifiers.Calculate(StatType.AttackMultiplier, baseAttackMultiplier));

        public float MoveSpeed =>
            ClampStat(StatType.MoveSpeed,
                modifiers.Calculate(StatType.MoveSpeed, baseMoveSpeed));

        public int MaxHP =>
            Mathf.RoundToInt(ClampStat(StatType.MaxHP,
                modifiers.Calculate(StatType.MaxHP, baseMaxHP)));

        public float ProjectileSpeedBonus =>
            modifiers.Calculate(StatType.ProjectileSpeed, baseProjectileSpeed);

        public int ProjectileCountBonus =>
            Mathf.FloorToInt(modifiers.Calculate(StatType.ProjectileCount, baseProjectileCount));

        public float SkillRangeBonus =>
            modifiers.Calculate(StatType.SkillRange, baseSkillRange);

        /// <summary>
        /// 쿨타임 감소 비율 (Add만 합산). Multiply는 GetEffectiveCooldown에서 별도 적용.
        /// </summary>
        public float CooldownReduction =>
            ClampStat(StatType.CooldownReduction,
                baseCooldownReduction + modifiers.GetAddTotal(StatType.CooldownReduction));

        public float KnockbackMultiplier =>
            modifiers.Calculate(StatType.Knockback, baseKnockback);

        public float CritDamageMultiplier =>
            modifiers.Calculate(StatType.CritDamage, baseCritDamage);

        /// <summary>치명타 확률 (0~1). 0.05 = 5%.</summary>
        public float CritChanceProbability =>
            Mathf.Clamp01(modifiers.Calculate(StatType.CritChance, baseCritChance));

        public float ExpMultiplier =>
            modifiers.Calculate(StatType.ExpMultiplier, baseExpMultiplier);

        public float DefenseMultiplier =>
            modifiers.Calculate(StatType.Defense, baseDefenseMultiplier);

        public float HealMultiplier =>
            modifiers.Calculate(StatType.HealMultiplier, baseHealMultiplier);

        public float SkillDurationBonus =>
            modifiers.Calculate(StatType.SkillDuration, baseSkillDuration);

        /// <summary>HP/초. HealMultiplier 영향 받지 않음.</summary>
        public float HpRegen =>
            Mathf.Max(0f, modifiers.Calculate(StatType.HpRegen, baseHpRegen));

        /// <summary>피격 후 무적 시간 (초).</summary>
        public float IFrameDuration =>
            Mathf.Max(0f, modifiers.Calculate(StatType.IFrameDuration, baseIFrameDuration));

        // ===== 이벤트 =====
        /// <summary>스탯 재계산 완료 시 발생. UI 갱신, 이동속도 적용 등.</summary>
        public event Action OnStatsChanged;

        // ===== 참조 캐시 =====
        private SkillManager skillManager;

        private void Awake()
        {
            skillManager = GetComponentInChildren<SkillManager>();
        }

        // [Step 1-3] OnPassiveChanged 구독 제거.
        // SkillManager가 패시브 변경 시 RegisterPassive/UnregisterPassive를 직접 호출합니다.
        // OnEnable/OnDisable에서 하던 이벤트 구독은 더 이상 불필요.

        // ===== Modifier 공개 API =====

        /// <summary>
        /// modifier 등록 (동일 source+statType이면 교체).
        /// 등록 후 Recalculate()를 별도 호출해야 이벤트가 발생합니다.
        /// 여러 modifier를 한 번에 등록할 때 Recalculate를 마지막에 한 번만 호출하세요.
        /// </summary>
        public void AddModifier(StatModifier modifier)
        {
            modifiers.AddOrReplace(modifier);
        }

        /// <summary>
        /// source가 일치하는 모든 modifier 제거.
        /// 제거 후 Recalculate()를 별도 호출해야 이벤트가 발생합니다.
        /// </summary>
        public int RemoveModifiersBySource(string source)
        {
            return modifiers.RemoveBySource(source);
        }

        /// <summary>
        /// source 접두사가 일치하는 모든 modifier 제거.
        /// 예: RemoveModifiersByPrefix("passive_") → 모든 패시브 보너스 제거.
        /// </summary>
        public int RemoveModifiersByPrefix(string prefix)
        {
            return modifiers.RemoveBySourcePrefix(prefix);
        }

        /// <summary>
        /// modifier의 source를 변경. 진화 시 패시브 → 진화 승계 용도.
        /// </summary>
        public int ReplaceModifierSource(string oldSource, string newSource)
        {
            return modifiers.ReplaceSource(oldSource, newSource);
        }

        /// <summary>
        /// 특정 source의 modifier가 존재하는지 확인.
        /// </summary>
        public bool HasModifierSource(string source)
        {
            return modifiers.HasSource(source);
        }

        /// <summary>
        /// 스탯 변경 이벤트 발행. modifier 등록/해제 후 호출.
        /// 재진입 방지 포함 — 계산 도중 다시 호출되면 무시됨.
        /// </summary>
        public void Recalculate()
        {
            if (isRecalculating)
            {
                Debug.LogWarning("[PlayerStats] Recalculate 재진입 차단");
                return;
            }

            isRecalculating = true;
            try
            {
                // modifier 컬렉션은 이미 최신 상태 — 계산은 프로퍼티 접근 시 수행.
                // 여기서는 이벤트 발행만 담당.
            }
            finally
            {
                isRecalculating = false;
            }

            OnStatsChanged?.Invoke();
            Debug.Log($"[PlayerStats] Recalculate — ATK:{AttackMultiplier:F2}, " +
                      $"SPD:{MoveSpeed:F1}, Modifiers:{modifiers.Count}개");
        }

        // ===== 하위 호환: RecalculateAll =====

        /// <summary>
        /// 보유 패시브 전체를 순회하여 modifier를 재등록.
        /// SkillManager.OnPassiveChanged 이벤트에서 호출.
        ///
        /// Step 1-3 이후에는 패시브가 직접 modifier를 등록하므로,
        /// 이 메서드는 ChaosSkillManager의 RecalculateModifiers()에서
        /// 이벤트 발행 용도로만 사용될 예정.
        /// </summary>
        public void RecalculateAll()
        {
            if (isRecalculating)
            {
                Debug.LogWarning("[PlayerStats] RecalculateAll 재진입 차단");
                return;
            }

            isRecalculating = true;
            try
            {
                // 패시브 modifier 전부 제거 후 재등록
                modifiers.RemoveBySourcePrefix("passive_");

                if (skillManager == null)
                    skillManager = GetComponentInChildren<SkillManager>();
                if (skillManager == null) return;

                var passives = skillManager.GetSkillsByType(SkillType.Passive);
                foreach (var skill in passives)
                {
                    if (skill == null || skill.Data == null) continue;
                    RegisterPassive(skill.Data, skill.Level);
                }
            }
            finally
            {
                isRecalculating = false;
            }

            OnStatsChanged?.Invoke();
            Debug.Log($"[PlayerStats] RecalculateAll — ATK:{AttackMultiplier:F2}, " +
                      $"SPD:{MoveSpeed:F1}, ProjSpd:{ProjectileSpeedBonus:F1}, " +
                      $"Modifiers:{modifiers.Count}개");
        }

        // ===== 패시브 스킬 공개 API (SkillManager에서 호출) =====

        /// <summary>
        /// 패시브 스킬의 modifier를 등록 또는 갱신.
        /// 동일 skillId의 modifier가 이미 있으면 새 값으로 교체 (레벨업).
        /// 호출 후 Recalculate()를 별도로 호출해야 이벤트가 발생합니다.
        /// </summary>
        public void RegisterPassive(SkillData data, int level)
        {
            StatType? statType = MapPassiveToStatType(data.bonusType);
            if (statType == null) return;

            float value = data.bonusPerLevel * level;
            string source = $"passive_{data.skillId}";

            // AttackMultiplier 는 데미지 공식 `(skillBase + Σ% × skillBase) × ...` 경로라
            // 패시브 "공격력 +X" 의도를 유지하려면 PercentBonus 로 등록해야 함.
            // (Add 로 등록 시 "+0.2 플랫 데미지" 가 돼 무의미해짐.)
            // 다른 스탯(MoveSpeed/MaxHP/...) 은 기존 `.Calculate()` 경로라 Add 가 의도한 플랫 가산.
            ModifierOp op = (statType.Value == StatType.AttackMultiplier)
                ? ModifierOp.PercentBonus
                : ModifierOp.Add;

            // Defense 는 SO 입력 의도가 "방어력 +5% (받는 데미지 -5%)" 이지만,
            // 내부 계산은 DefenseMultiplier (받는 데미지 배율) 이다.
            // 부호를 반전시켜 등록하면 입력 0.05 → modifier -0.05 → 받는 데미지 95%.
            if (statType.Value == StatType.Defense)
                value = -value;

            modifiers.AddOrReplace(new StatModifier(
                source,
                statType.Value,
                op,
                value
            ));
        }

        /// <summary>
        /// 패시브 스킬의 modifier를 제거.
        /// 호출 후 Recalculate()를 별도로 호출해야 이벤트가 발생합니다.
        /// </summary>
        public void UnregisterPassive(int skillId)
        {
            modifiers.RemoveBySource($"passive_{skillId}");
        }

        /// <summary>
        /// 진화 시 패시브 modifier를 진화 스킬로 승계.
        /// source를 "passive_{passiveId}" → "evolution_{evolvedSkillId}"로 변경.
        /// 이후 UnregisterPassive는 이미 rename된 modifier를 찾지 못하므로 안전.
        /// </summary>
        /// <returns>승계된 modifier 수</returns>
        public int PreservePassiveForEvolution(int passiveSkillId, int evolvedSkillId)
        {
            string oldSource = $"passive_{passiveSkillId}";
            string newSource = $"evolution_{evolvedSkillId}";
            int count = modifiers.ReplaceSource(oldSource, newSource);
            if (count > 0)
                Debug.Log($"[PlayerStats] 패시브 승계: passive_{passiveSkillId} → evolution_{evolvedSkillId} ({count}개)");
            return count;
        }

        /// <summary>
        /// PassiveBonusType → StatType 매핑.
        /// None이면 null 반환.
        /// Step 3 (SkillData 상속 분리) 이후에는 이 매핑이 불필요해질 수 있음.
        /// </summary>
        private static StatType? MapPassiveToStatType(PassiveBonusType bonusType)
        {
            switch (bonusType)
            {
                case PassiveBonusType.ProjectileSpeed:    return StatType.ProjectileSpeed;
                case PassiveBonusType.ProjectileCount:    return StatType.ProjectileCount;
                case PassiveBonusType.SkillRange:         return StatType.SkillRange;
                case PassiveBonusType.SkillDuration:      return StatType.SkillDuration;
                case PassiveBonusType.AttackMultiplier:    return StatType.AttackMultiplier;
                case PassiveBonusType.Knockback:          return StatType.Knockback;
                case PassiveBonusType.HealingMultiplier:  return StatType.HealMultiplier;
                case PassiveBonusType.CritDamage:         return StatType.CritDamage;
                case PassiveBonusType.CritChance:         return StatType.CritChance;
                case PassiveBonusType.CooldownReduction:  return StatType.CooldownReduction;
                case PassiveBonusType.MaxHP:              return StatType.MaxHP;
                case PassiveBonusType.MoveSpeed:          return StatType.MoveSpeed;
                case PassiveBonusType.Defense:            return StatType.Defense;
                case PassiveBonusType.ExpMultiplier:      return StatType.ExpMultiplier;
                case PassiveBonusType.HpRegen:            return StatType.HpRegen;
                case PassiveBonusType.IFrameDuration:     return StatType.IFrameDuration;
                default:                                  return null;
            }
        }

        // ===== Clamp 정책 =====

        /// <summary>
        /// StatType별 상한/하한 적용.
        /// TODO: [밸런싱] GameplayConfig SO에서 Clamp 수치를 읽도록 변경 검토.
        /// </summary>
        private float ClampStat(StatType type, float value)
        {
            switch (type)
            {
                case StatType.CooldownReduction:
                    return Mathf.Clamp(value, 0f, 0.8f); // TODO: [밸런싱] 상한 확정
                case StatType.MoveSpeed:
                    return Mathf.Max(value, 0.1f); // TODO: [밸런싱] 하한 확정
                case StatType.MaxHP:
                    return Mathf.Max(value, 1f);
                case StatType.AttackMultiplier:
                    return Mathf.Max(value, 0f);
                default:
                    return value;
            }
        }

        // ===== 외부 유틸리티 (기존 인터페이스 유지) =====

        /// <summary>
        /// 실제 쿨다운 계산.
        /// CDR(Add modifiers) + 쿨다운 배율(Multiply modifiers, 혼돈 스킬 등) 모두 반영.
        /// 공식: baseCooldown × (1 - CDR비율) × 쿨다운배율
        /// </summary>
        public float GetEffectiveCooldown(float baseCooldown)
        {
            float cdr = CooldownReduction;
            float cooldownMul = modifiers.GetMultiplicativeTotal(StatType.CooldownReduction);
            return baseCooldown * (1f - cdr) * cooldownMul;
        }

        public int GetEffectiveProjectileCount(int baseCount)
        {
            return baseCount + ProjectileCountBonus;
        }

        public float GetEffectiveProjectileSpeed(float baseSpeed)
        {
            return baseSpeed + ProjectileSpeedBonus;
        }

        // ===== 필터 적용 스탯 접근 (스킬별 패시브 적용 필터, N18 multiplier 차등) =====
        //
        // 패턴: skillData.GetStatMultiplier(type) 로 비율 (0=미적용 / 1=100% / 0.5=50% / 1.5=150%) 얻은 후
        //       보너스 부분에만 곱. 스킬 base / 캐릭터 base 는 보호.
        //   actualBonus = finalValue - baseValue
        //   filtered    = baseValue + actualBonus * mult

        /// <summary>투사체 개수 (필터 적용).</summary>
        public int GetFilteredProjectileCount(int baseCount, SkillData skillData)
        {
            int actualBonus = ProjectileCountBonus - baseProjectileCount;
            float mult = skillData != null ? skillData.GetStatMultiplier(StatType.ProjectileCount) : 1f;
            return baseCount + Mathf.RoundToInt(actualBonus * mult);
        }

        /// <summary>투사체 속도 (필터 적용).</summary>
        public float GetFilteredProjectileSpeed(float baseSpeed, SkillData skillData)
        {
            float actualBonus = ProjectileSpeedBonus - baseProjectileSpeed;
            float mult = skillData != null ? skillData.GetStatMultiplier(StatType.ProjectileSpeed) : 1f;
            return baseSpeed + actualBonus * mult;
        }

        /// <summary>공격력 배율 (필터 적용). 데미지 경로는 <see cref="ApplyAttackTo"/> 사용 권장 — 이 프로퍼티는
        /// 디버그/HUD 표시나 조건부 로직용. 새 데미지 공식이 skillBase 에 의존하므로 단일 "배율" 은 정확한 수치가 아님.</summary>
        public float GetFilteredAttackMultiplier(SkillData skillData)
        {
            float mult = skillData != null ? skillData.GetStatMultiplier(StatType.AttackMultiplier) : 1f;
            return baseAttackMultiplier + (AttackMultiplier - baseAttackMultiplier) * mult;
        }

        /// <summary>
        /// 스킬 데미지 공식. SkillExecutor.BuildContext 에서 ctx.damage 산출 시 호출.
        ///
        /// 공식:
        ///   final = (skillBase + ΣAdd × mult + skillBase × ΣPercentBonus × mult) × (1 + (ΠMultiplicative - 1) × mult) × baseAttackMultiplier
        ///
        /// N18 statOverrides multiplier: mult = skillData.GetStatMultiplier(AttackMultiplier).
        ///   default(미나열) = 1 → 전체 보너스 100% 적용
        ///   override mult=0 → 모든 보너스 미적용, skillBase * baseAttackMultiplier 만
        ///   override mult=0.5 → 보너스 50% 만 적용 / mult=1.5 → 150% 강화
        /// </summary>
        public float ApplyAttackTo(float skillBase, SkillData skillData)
        {
            float mult = skillData != null ? skillData.GetStatMultiplier(StatType.AttackMultiplier) : 1f;
            if (mult <= 0f) return skillBase * baseAttackMultiplier;

            float adds    = modifiers.GetAddTotal(StatType.AttackMultiplier) * mult;
            float percent = modifiers.GetPercentBonusTotal(StatType.AttackMultiplier) * mult;
            float multTotal = modifiers.GetMultiplicativeTotal(StatType.AttackMultiplier);
            // multiplicative 는 1.0 base 라 (값-1)*mult + 1 로 차등 적용
            float multAdjusted = 1f + (multTotal - 1f) * mult;

            return (skillBase + adds + skillBase * percent) * multAdjusted * baseAttackMultiplier;
        }

        /// <summary>스킬 범위 보너스 (필터 적용).</summary>
        public float GetFilteredSkillRangeBonus(SkillData skillData)
        {
            float actualBonus = SkillRangeBonus - baseSkillRange;
            float mult = skillData != null ? skillData.GetStatMultiplier(StatType.SkillRange) : 1f;
            return baseSkillRange + actualBonus * mult;
        }

        /// <summary>스킬 유지 시간 보너스 (필터 적용).</summary>
        public float GetFilteredSkillDurationBonus(SkillData skillData)
        {
            float actualBonus = SkillDurationBonus - baseSkillDuration;
            float mult = skillData != null ? skillData.GetStatMultiplier(StatType.SkillDuration) : 1f;
            return baseSkillDuration + actualBonus * mult;
        }

        /// <summary>넉백 배율 (필터 적용).</summary>
        public float GetFilteredKnockbackMultiplier(SkillData skillData)
        {
            float actualBonus = KnockbackMultiplier - baseKnockback;
            float mult = skillData != null ? skillData.GetStatMultiplier(StatType.Knockback) : 1f;
            return baseKnockback + actualBonus * mult;
        }

        /// <summary>회복량 배율 (필터 적용).</summary>
        public float GetFilteredHealMultiplier(SkillData skillData)
        {
            float actualBonus = HealMultiplier - baseHealMultiplier;
            float mult = skillData != null ? skillData.GetStatMultiplier(StatType.HealMultiplier) : 1f;
            return baseHealMultiplier + actualBonus * mult;
        }

        // ===== 캐릭터 데이터 연동 =====

        /// <summary>
        /// CharacterData의 base 스탯으로 전체 base 값 덮어쓰기.
        /// PlayerStub.Initialize()에서 호출.
        /// </summary>
        public void ApplyCharacterBase(CharacterData data)
        {
            if (data == null) return;

            baseAttackMultiplier = data.attackMultiplier;
            baseMoveSpeed = data.moveSpeed;
            baseMaxHP = data.maxHP;
            baseProjectileSpeed = data.projectileSpeed;
            baseProjectileCount = data.projectileCount;
            baseSkillRange = data.skillRange;
            baseCooldownReduction = data.cooldownReduction;
            baseKnockback = data.knockback;
            baseCritDamage = data.critDamage;
            baseCritChance = data.critChance;
            baseExpMultiplier = data.expMultiplier;
            // CharacterData.defenseBonus 는 패시브와 동일한 양수 컨벤션 ("방어력 +N% = 강함").
            // 내부 baseDefenseMultiplier 는 "받는 데미지 배율" 이므로 1f 에서 차감해 변환.
            baseDefenseMultiplier = 1f - data.defenseBonus;
            baseHealMultiplier = data.healMultiplier;
            baseSkillDuration = data.skillDuration;
            baseHpRegen = data.hpRegen;
            baseIFrameDuration = data.iFrameDuration;

            OnStatsChanged?.Invoke();
            Debug.Log($"[PlayerStats] 캐릭터 base 스탯 적용: {data.displayName}");
        }

        // ===== 디버그 =====

        /// <summary>현재 등록된 모든 modifier 목록. 디버그 오버레이용.</summary>
        public string GetModifierDebugString()
        {
            return modifiers.ToDebugString();
        }

        /// <summary>현재 modifier 수.</summary>
        public int ModifierCount => modifiers.Count;

        /// <summary>
        /// AttackMultiplier 의 3-op 분해값. 디버그 표시용.
        /// ApplyAttackTo(skillBase) 가 어떻게 합쳐지는지 눈으로 확인.
        /// </summary>
        public void GetAttackBreakdown(out float flatAdd, out float percentBonusSum, out float multiplicative)
        {
            flatAdd         = modifiers.GetAddTotal(StatType.AttackMultiplier);
            percentBonusSum = modifiers.GetPercentBonusTotal(StatType.AttackMultiplier);
            multiplicative  = modifiers.GetMultiplicativeTotal(StatType.AttackMultiplier);
        }

        /// <summary>캐릭터 고유 공격 배율 (baseAttackMultiplier). 디버그 표시용.</summary>
        public float BaseAttackMultiplierForDebug => baseAttackMultiplier;
    }
}