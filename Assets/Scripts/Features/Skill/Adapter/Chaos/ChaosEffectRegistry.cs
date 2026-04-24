using System.Collections.Generic;
using SwDreams.Features.Skill.Adapter.Data;
using UnityEngine;

namespace SwDreams.Features.Skill.Adapter.Chaos
{
    /// <summary>
    /// 혼돈 효과 타입별 <see cref="IChaosEffectHandler"/> 등록소.
    /// ChaosSkillManager 가 초기화 시 RegisterDefaults 로 기본 handler 들을 등록.
    ///
    /// 점진 이전 패턴: 모든 혼돈 스킬이 handler 로 이전되기 전까지 switch 와 공존.
    /// Registry 에 handler 가 등록된 타입만 dispatch 되고, 그 외는 매니저가 기존 로직으로 처리.
    /// </summary>
    public class ChaosEffectRegistry
    {
        private readonly Dictionary<ChaosEffectType, IChaosEffectHandler> map =
            new Dictionary<ChaosEffectType, IChaosEffectHandler>();

        public void Register(IChaosEffectHandler handler)
        {
            if (handler == null) return;
            map[handler.EffectType] = handler;
        }

        public bool TryGet(ChaosEffectType type, out IChaosEffectHandler handler)
            => map.TryGetValue(type, out handler);

        /// <summary>handler 가 등록된 타입이면 true — ChaosSkillManager 의 switch 우회 판정.</summary>
        public bool HasHandler(ChaosEffectType type) => map.ContainsKey(type);

        public IReadOnlyCollection<IChaosEffectHandler> All => map.Values;

        /// <summary>
        /// 기본 handler 등록. 점진 이전에 따라 항목이 늘어난다.
        /// Phase 8-B 1 단계: 인프라만. 2 단계 이후 각 혼돈을 handler 로 이전하며 등록 추가.
        /// 현재 등록 대상 없음 — ChaosSkillManager 의 기존 switch 가 전 혼돈을 처리.
        /// </summary>
        public void RegisterDefaults()
        {
            // TODO Phase 8-B2: ChainExplosion / Gambler 등 Category C/D 혼돈 이전 시 여기에 등록.
        }
    }
}
