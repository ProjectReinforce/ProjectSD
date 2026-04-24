using System;
using System.Collections.Generic;

namespace SwDreams.Shared.Domain.Interfaces
{
    /// <summary>
    /// 스킬 컬렉션에 런타임 트리거 효과를 뿌리려는 주입자(정수/무기) 가 의존하는 포트.
    /// SkillManager(Skill.Adapter) 가 구현 선언. 주입자는 Skill 타입 자체를 알 필요 없음.
    ///
    /// 의도: PlayerEssenceInventory / PlayerWeaponInventory 가 Skill.Adapter 를 직접 의존하지 않도록
    /// "스킬 목록" 을 sink 관점으로만 노출.
    /// </summary>
    public interface ISkillRegistry
    {
        /// <summary>
        /// 현재 장착된 스킬들의 <see cref="IRuntimeEffectSink"/>. null 엔트리 제외.
        /// 내부 캐시를 반환 — caller 는 수정 금지, 순회 중 Add/Remove 가 일어날 수 있으므로 snapshot 용도로만.
        /// </summary>
        IReadOnlyList<IRuntimeEffectSink> EffectSinks { get; }

        /// <summary>
        /// 신규 스킬 획득 시 해당 스킬의 sink 를 전달 (없으면 호출 안 됨).
        /// 주입자는 기존 보유 효과를 이 sink 하나에만 재주입하는 데 사용.
        /// </summary>
        event Action<IRuntimeEffectSink> OnSinkAdded;
    }
}
