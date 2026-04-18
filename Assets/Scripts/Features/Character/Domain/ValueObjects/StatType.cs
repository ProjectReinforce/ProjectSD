namespace SwDreams.Features.Character.Domain.ValueObjects
{
    /// <summary>
    /// 통합 스탯 타입. 패시브/혼돈/진화/캐릭터 등 모든 스탯 소스에서 사용.
    /// 기존 PassiveBonusType을 대체합니다.
    /// 
    /// [Phase 7 리팩토링] Step 1-1
    /// </summary>
    public enum StatType
    {
        AttackMultiplier,       // 공격력 배율
        MoveSpeed,              // 이동속도
        MaxHP,                  // 최대 체력
        ProjectileSpeed,        // 투사체 속도
        ProjectileCount,        // 투사체 개수
        SkillRange,             // 스킬 범위
        SkillDuration,          // 스킬 유지 시간
        Knockback,              // 넉백
        HealMultiplier,         // 회복량 배율
        CritDamage,             // 치명타 데미지
        CooldownReduction,      // 쿨타임 감소
        Defense,                // 방어력
        ExpMultiplier,          // 경험치 배율
        CritChance,             // 치명타 확률 (무기/정수 시스템용)
        LifeSteal               // 흡혈 (무기/정수 시스템용)
    }

    /// <summary>
    /// Modifier 연산 타입.
    /// 계산 순서: (Base + 모든 Add) × 모든 Multiply
    /// </summary>
    public enum ModifierOp
    {
        /// <summary>기본값에 가산. 패시브 보너스, 캐릭터 보정 등.</summary>
        Add,

        /// <summary>최종값에 곱연산. 혼돈 스킬 배율 등. 기본값 1.0.</summary>
        Multiply
    }
}