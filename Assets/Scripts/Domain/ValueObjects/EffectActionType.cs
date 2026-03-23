namespace SwDreams.Domain.ValueObjects
{
    /// <summary>
    /// 트리거 발동 시 실행할 효과 종류.
    /// EffectActionRegistry에 핸들러를 등록하여 실행.
    /// 새 효과 추가 시 enum 값 + 핸들러 등록만 하면 됨.
    ///
    /// [Phase 7 리팩토링] Step 3-1
    /// </summary>
    public enum EffectActionType
    {
        /// <summary>추가 데미지. primary=데미지, secondary=범위(0이면 단일 대상).</summary>
        DealDamage,

        /// <summary>범위 폭발. primary=반경, secondary=데미지 배율(1.0=스킬 데미지 100%).</summary>
        Explode,

        /// <summary>주변 적에게 체인. primary=체인 횟수, secondary=탐색 반경.</summary>
        Chain,

        /// <summary>지속 데미지 부여. primary=틱당 데미지, secondary=지속시간, tertiary=틱 간격.</summary>
        ApplyDoT,

        /// <summary>슬로우 부여. primary=슬로우 배율(0.5=50% 감속), secondary=지속시간.</summary>
        ApplySlow,

        /// <summary>끌어당김. primary=반경, secondary=힘.</summary>
        Pull,

        /// <summary>스킬 재발동. primary=재발동 쿨다운(0이면 즉시).</summary>
        Refire,

        /// <summary>추가 투사체 생성. primary=개수, secondary=데미지 배율.</summary>
        SpawnProjectile,

        /// <summary>적에게 디버프 마커. primary=받는 피해 증가 배율, secondary=지속시간.</summary>
        ApplyVulnerability,

        /// <summary>자신 회복. primary=회복량(고정), secondary=회복량(스킬 데미지 비율).</summary>
        HealSelf,

        /// <summary>HP 비율 이하 즉사 (보스 제외). primary=임계값(0.15=15%), secondary=범위(0=단일).</summary>
        Execute
    }
}
