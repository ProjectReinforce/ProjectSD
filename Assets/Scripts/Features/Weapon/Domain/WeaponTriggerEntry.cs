using System;
using SwDreams.Features.Skill.Domain.ValueObjects;

namespace SwDreams.Features.Weapon.Domain
{
    /// <summary>
    /// 무기가 스킬에 주입하는 트리거 효과 엔트리. SkillTriggerEffect 에 isUnique 옵션을 얹는 래퍼.
    ///
    /// SkillTriggerEffect 자체는 Skill Feature 의 Domain VO 라 무기 전용 관심사를 거기에 끼워 넣지 않는다.
    /// 대신 무기 쪽에서 이 래퍼로 들고 있다가 IRuntimeEffectSink 로 주입 시점에 source 만 달리 준다.
    ///
    /// 주입 네이밍 (PlayerWeaponInventory 에서 조립):
    ///   unique = true  → "weapon_{id}_u_e{entryIdx}"       — 슬롯 무관, 여러 개 장착해도 1회분
    ///   unique = false → "weapon_{id}_s{slotUid}_e{entryIdx}" — 슬롯별 독립 스택
    /// </summary>
    [Serializable]
    public struct WeaponTriggerEntry
    {
        public SkillTriggerEffect effect;

        /// <summary>
        /// true 면 같은 무기 중복 장착 시에도 이 트리거는 1 인스턴스만 등록된다.
        /// </summary>
        public bool isUnique;

        public WeaponTriggerEntry(SkillTriggerEffect effect, bool isUnique = false)
        {
            this.effect = effect;
            this.isUnique = isUnique;
        }

        public override string ToString()
        {
            string uniq = isUnique ? " [U]" : string.Empty;
            return $"{effect}{uniq}";
        }
    }
}
