namespace SwDreams.Domain.ValueObjects
{
    /// <summary>
    /// 스킬 추가 효과의 발동 시점.
    /// SkillTriggerSystem에서 FireTrigger(type) 호출 시 매칭.
    ///
    /// [Phase 7 리팩토링] Step 3-1
    /// </summary>
    public enum TriggerType
    {
        /// <summary>스킬 발사/시전 시.</summary>
        OnFire,

        /// <summary>적에게 적중 시 (투사체 충돌, 장판 틱 등).</summary>
        OnHit,

        /// <summary>적 처치 시.</summary>
        OnKill,

        /// <summary>투사체/장판 소멸 시.</summary>
        OnExpire,

        /// <summary>주기적 발동 (간격은 EffectParams.secondary).</summary>
        OnInterval,

        /// <summary>플레이어 피격 시.</summary>
        OnPlayerHit
    }
}
