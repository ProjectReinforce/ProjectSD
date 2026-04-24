using SwDreams.Features.Character.Domain.ValueObjects;

namespace SwDreams.Shared.Domain.Interfaces
{
    /// <summary>
    /// PlayerStats 의 쓰기 경로 포트. 무기/정수/버프 등이 stat modifier 를 등록/해제할 때 의존.
    /// PlayerStats(Character.Adapter) 가 구현 선언. 주입자는 이 포트만 알면 됨.
    ///
    /// source 네이밍 규약은 각 주입자 책임:
    ///   passive_{skillId}                           — SkillManager
    ///   essence_{type}_{slot} / essence_combo_{...} — PlayerEssenceInventory
    ///   weapon_{id}_[s{slotUid}_]e{entryIdx}        — PlayerWeaponInventory
    ///   chaos_{effect}                              — ChaosSkillManager
    ///   buff_{id}                                   — 일시 버프
    /// </summary>
    public interface IPlayerStatsMutator
    {
        /// <summary>동일 source+StatType 조합이면 교체 (AddOrReplace 의미).</summary>
        void AddModifier(StatModifier modifier);

        /// <returns>제거된 modifier 수</returns>
        int RemoveModifiersBySource(string source);

        /// <returns>제거된 modifier 수</returns>
        int RemoveModifiersByPrefix(string prefix);

        /// <summary>
        /// 변경 이벤트 발행. 여러 modifier 를 등록/제거한 뒤 마지막에 한 번 호출해
        /// UI/이동속도 적용 등이 한 번만 발생하도록 한다.
        /// </summary>
        void Recalculate();
    }
}
