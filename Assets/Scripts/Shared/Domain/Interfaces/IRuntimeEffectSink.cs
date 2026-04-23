using SwDreams.Features.Skill.Domain.ValueObjects;

namespace SwDreams.Shared.Domain.Interfaces
{
    /// <summary>
    /// 스킬에 런타임 트리거 효과를 주입/회수하는 수용자(Sink) 포트.
    /// SkillTriggerSystem 이 구현. Essence/Weapon/Buff 등 주입자는 이 포트만 의존.
    ///
    /// source 네이밍 컨벤션:
    ///   essence_{type}_{slot}   — 정수 (슬롯별 구분)
    ///   essence_combo_{combo}   — 정수 조합 효과 (미구현)
    ///   weapon_{id}             — 무기 트리거 부여 (슬롯 구분 필요 시 weapon_{id}_{slot})
    ///   chaos_{id}              — 혼돈 스킬
    ///   buff_{id}               — 일시 버프
    ///
    /// 동일 source+trigger+action 조합은 교체 의미 (AddRuntimeEffect 구현 계약).
    /// </summary>
    public interface IRuntimeEffectSink
    {
        /// <summary>
        /// 런타임 효과 추가. 같은 source+trigger+action 조합이면 교체.
        /// </summary>
        void AddRuntimeEffect(string source, SkillTriggerEffect effect);

        /// <summary>
        /// source 가 정확히 일치하는 런타임 효과 제거.
        /// </summary>
        /// <returns>제거된 효과 수</returns>
        int RemoveRuntimeEffects(string source);

        /// <summary>
        /// source 접두사가 일치하는 모든 런타임 효과 제거.
        /// 예: RemoveByPrefix("weapon_") — 모든 무기 기여분 제거.
        /// </summary>
        /// <returns>제거된 효과 수</returns>
        int RemoveByPrefix(string prefix);
    }
}
