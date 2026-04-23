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
    ///
    /// 계산 공식:
    ///   Final = (Base + ΣAdd) × (1 + ΣPercentBonus) × ΠMultiplicative
    ///
    /// - Add: 기본값에 플랫 가산 (예: 공격력 +10).
    /// - PercentBonus: 가산적 % 스택 (예: 여러 무기의 "+10%" 가 선형 누적 → +20%).
    ///   Value=0 이 기본값(기여 없음). 음수도 허용(감산 %).
    /// - Multiplicative: 원 배율을 곱함. 유리대포처럼 "무조건 n 배" 의도를 보존하고자 할 때.
    ///   Value=1 이 기본값(변동 없음).
    /// </summary>
    public enum ModifierOp
    {
        /// <summary>기본값에 플랫 가산. 패시브 보너스, 캐릭터 보정 등.</summary>
        Add,

        /// <summary>가산적 % 기여. 여러 소스가 있으면 값이 합산된 뒤 (1 + Σ) 로 적용.
        /// 예) 무기 A +0.1, 무기 B +0.1 → (1 + 0.2) = ×1.2.</summary>
        PercentBonus,

        /// <summary>원 배율 곱 스택. 기본값 1.0.
        /// 예) 혼돈 유리대포 ×0.5 HP — 다른 %HP 아이템 영향 없이 항상 원값의 절반.</summary>
        Multiplicative
    }
}