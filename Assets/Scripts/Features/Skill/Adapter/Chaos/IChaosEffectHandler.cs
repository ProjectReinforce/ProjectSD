using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Shared.Domain.Interfaces;
using SwDreams.Shared.Domain.ValueObjects;

namespace SwDreams.Features.Skill.Adapter.Chaos
{
    /// <summary>
    /// 개별 혼돈 효과의 로직 단위. ChaosSkillManager 가 switch/하드코딩 대신
    /// <see cref="ChaosEffectRegistry"/> 를 경유해 handler 에 dispatch.
    ///
    /// 각 handler 는 자기만의:
    /// - 상태 (활성/비활성)
    /// - 파라미터 해석 (SO paramsByRarity + rolledRarity)
    /// - 훅 구독 (hookBus.OnEnemyKilled 등)
    /// - modifier 등록/해제 (IPlayerStatsMutator 포트)
    /// 을 담당.
    /// </summary>
    public interface IChaosEffectHandler
    {
        /// <summary>handler 가 담당하는 혼돈 효과 타입. Registry 의 dispatch 키.</summary>
        ChaosEffectType EffectType { get; }

        /// <summary>
        /// 혼돈 효과 최초 적용. ChaosSkillManager 에 장착 시 1 회 호출.
        /// <paramref name="ctx"/> 는 handler 가 필요로 하는 의존성 번들 (player root, stats, hook bus 등).
        /// </summary>
        void Apply(ChaosSkillData data, Rarity rolledRarity, ChaosHandlerContext ctx);

        /// <summary>
        /// 해제 (호스트 마이그레이션 복구 등). 훅 구독 해제 + modifier 제거 책임.
        /// 현재 게임 내 해제 경로는 없으나 예약.
        /// </summary>
        void Remove(ChaosHandlerContext ctx);
    }

    /// <summary>
    /// handler 에 주입되는 의존성 집합. 새 의존 추가 시 필드 하나 늘리면 모든 handler 가 공유.
    /// </summary>
    public struct ChaosHandlerContext
    {
        public UnityEngine.Transform playerRoot;
        public IPlayerStatsMutator stats;
        public IChaosHookBus hookBus;
    }
}
