using System;
using SwDreams.Features.Character.Domain.ValueObjects;
using SwDreams.Features.Character.Adapter.Data;
using SwDreams.Features.Character.Adapter;
using SwDreams.Features.Skill.Adapter;
using SwDreams.Features.Skill.Adapter.Data;
using UnityEngine;
using SwDreams.Shared.Data;

namespace SwDreams.Features.Character.Adapter
{
    /// <summary>
    /// 플레이어 스탯 관리. StatModifierCollection 기반.
    ///
    /// 구조: Base (인스펙터/CharacterData) + Modifier (패시브/혼돈/진화/무기 등) = Final
    /// 계산: (Base + 모든 Add) × 모든 Multiply, 이후 Clamp 적용
    ///
    /// 외부에서 modifier 등록/해제 → Recalculate() 호출 → OnStatsChanged 이벤트.
    ///
    /// [Phase 7 리팩토링] Step 1-2: 내부 저장소를 StatModifierCollection으로 전환.
    /// - 기존 bonusXxx 필드 제거
    /// - RecalculateAll()은 하위 호환을 위해 유지 (패시브 순회 → modifier 재등록)
    /// - ChaosSkillManager 헬퍼는 Step 1-4에서 modifier로 통합 예정
    /// </summary>
    public class PlayerStats : MonoBehaviour
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
        [SerializeField] private float baseExpMultiplier = 1f;
        [SerializeField] private float baseDefenseMultiplier = 1f;
        [SerializeField] private float baseHealMultiplier = 1f;
        [SerializeField] private float baseSkillDuration = 0f;

        // ===== Modifier 컬렉션 (패시브/혼돈/진화/무기/정수 등 모든 보정값) =====
        private readonly StatModifierCollection modifiers = new StatModifierCollection();

        // 재진입 방지
        private bool isRecalculating = false;

        // ===== Final 스탯 (외부 읽기 전용) =====

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

        public float ExpMultiplier =>
            modifiers.Calculate(StatType.ExpMultiplier, baseExpMultiplier);

        public float DefenseMultiplier =>
            modifiers.Calculate(StatType.Defense, baseDefenseMultiplier);

        public float HealMultiplier =>
            modifiers.Calculate(StatType.HealMultiplier, baseHealMultiplier);

        public float SkillDurationBonus =>
            modifiers.Calculate(StatType.SkillDuration, baseSkillDuration);

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

            modifiers.AddOrReplace(new StatModifier(
                source,
                statType.Value,
                ModifierOp.Add,
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
                case PassiveBonusType.CooldownReduction:  return StatType.CooldownReduction;
                case PassiveBonusType.MaxHP:              return StatType.MaxHP;
                case PassiveBonusType.MoveSpeed:          return StatType.MoveSpeed;
                case PassiveBonusType.Defense:            return StatType.Defense;
                case PassiveBonusType.ExpMultiplier:      return StatType.ExpMultiplier;
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
            float cooldownMul = modifiers.GetMultiplyTotal(StatType.CooldownReduction);
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

        // ===== 필터 적용 스탯 접근 (스킬별 패시브 적용 필터) =====

        /// <summary>
        /// SkillData.applicableStats 필터를 적용하여 스탯값 반환.
        /// 필터에 포함되지 않은 스탯은 base값만 반환 (보너스 미적용).
        /// filter가 null이거나 비어있으면 전부 적용 (하위 호환).
        /// </summary>
        public float GetFilteredStat(StatType type, float baseValue, SkillData skillData)
        {
            if (skillData == null || skillData.IsStatApplicable(type))
                return modifiers.Calculate(type, baseValue);

            return baseValue;
        }

        /// <summary>투사체 개수 (필터 적용).</summary>
        public int GetFilteredProjectileCount(int baseCount, SkillData skillData)
        {
            if (skillData == null || skillData.IsStatApplicable(StatType.ProjectileCount))
                return baseCount + ProjectileCountBonus;
            return baseCount;
        }

        /// <summary>투사체 속도 (필터 적용).</summary>
        public float GetFilteredProjectileSpeed(float baseSpeed, SkillData skillData)
        {
            if (skillData == null || skillData.IsStatApplicable(StatType.ProjectileSpeed))
                return baseSpeed + ProjectileSpeedBonus;
            return baseSpeed;
        }

        /// <summary>공격력 배율 (필터 적용).</summary>
        public float GetFilteredAttackMultiplier(SkillData skillData)
        {
            if (skillData == null || skillData.IsStatApplicable(StatType.AttackMultiplier))
                return AttackMultiplier;
            return baseAttackMultiplier;
        }

        /// <summary>스킬 범위 보너스 (필터 적용).</summary>
        public float GetFilteredSkillRangeBonus(SkillData skillData)
        {
            if (skillData == null || skillData.IsStatApplicable(StatType.SkillRange))
                return SkillRangeBonus;
            return baseSkillRange;
        }

        /// <summary>스킬 유지 시간 보너스 (필터 적용).</summary>
        public float GetFilteredSkillDurationBonus(SkillData skillData)
        {
            if (skillData == null || skillData.IsStatApplicable(StatType.SkillDuration))
                return SkillDurationBonus;
            return baseSkillDuration;
        }

        /// <summary>넉백 배율 (필터 적용).</summary>
        public float GetFilteredKnockbackMultiplier(SkillData skillData)
        {
            if (skillData == null || skillData.IsStatApplicable(StatType.Knockback))
                return KnockbackMultiplier;
            return baseKnockback;
        }

        /// <summary>회복량 배율 (필터 적용).</summary>
        public float GetFilteredHealMultiplier(SkillData skillData)
        {
            if (skillData == null || skillData.IsStatApplicable(StatType.HealMultiplier))
                return HealMultiplier;
            return baseHealMultiplier;
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
            baseExpMultiplier = data.expMultiplier;
            baseDefenseMultiplier = data.defenseMultiplier;
            baseHealMultiplier = data.healMultiplier;
            baseSkillDuration = data.skillDuration;

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
    }
}