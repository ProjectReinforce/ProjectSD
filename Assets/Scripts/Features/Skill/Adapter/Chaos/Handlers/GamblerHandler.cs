using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Shared.Domain.ValueObjects;

namespace SwDreams.Features.Skill.Adapter.Chaos.Handlers
{
    /// <summary>
    /// 도박꾼 혼돈 효과 핸들러.
    ///
    /// 효과 (설계 docs/game-design/skills/chaos/gambler.md):
    ///   매 레벨업 시 선택지 카드 전체가 상위 등급으로 등장.
    ///   효과 범위는 파티 전체 — 장착자 혼자가 아니라 모든 파티원의 레벨업에 적용.
    ///   구체 rarity bump 분포 (1~3 단계 상승 확률표) 는 설계 문서 참조, 밸런싱 단계.
    ///
    /// 구현 상태 (Phase 8-B3 + 소비자):
    ///   - 본 핸들러: "이 플레이어가 도박꾼 활성 여부" 플래그만 노출.
    ///   - 소비자: <c>LevelUpManager.ResolveGamblerOverride</c> 가 파티 전체
    ///     <see cref="ChaosSkillManager.IsGambler"/> 를 순회 → 활성 시
    ///     <c>GamblerRarityBumper.Bump</c> 로 rolledRarity 상승 후 RPC 송신.
    ///
    /// IChaosHookBus.LevelUpChoice 훅 구독은 의도적으로 안 함 — bump 로직이
    /// LevelUpManager (호스트 권위) 에 위치하므로 handler 측 훅 처리는 dead code.
    /// 추후 "choice modifier 추상화" 도입 시 이 핸들러로 bump 로직을 이전할 여지 있음.
    ///
    /// 파라미터: 없음 (ChaosSkillData.paramsByRarity 는 전 등급 (0,0,0)).
    /// 조회 경로: <see cref="ChaosEffectRegistry"/> 에서 핸들러 획득 후 <see cref="IsActive"/>.
    /// </summary>
    public class GamblerHandler : IChaosEffectHandler
    {
        public ChaosEffectType EffectType => ChaosEffectType.Gambler;

        /// <summary>장착 이후 true. 레벨업 선택지 생성부가 파티 전체 순회 시 조회.</summary>
        public bool IsActive { get; private set; }

        public void Apply(ChaosSkillData data, Rarity rolledRarity, ChaosHandlerContext ctx)
        {
            IsActive = true;
        }

        public void Remove(ChaosHandlerContext ctx)
        {
            IsActive = false;
        }
    }
}
