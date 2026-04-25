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
    /// 구현 상태 (Phase 8-B3):
    ///   본 핸들러는 "이 플레이어가 도박꾼 활성 여부" 플래그만 노출.
    ///   실제 rarity bump 소비자는 LevelUpManager (호스트 권위) 가
    ///   파티 전체 ChaosSkillManager 를 순회해 "한 명이라도 활성이면 bump" 판정 예정.
    ///   IChaosHookBus.LevelUpChoice 훅은 연결 지점으로 예약만 — 현재 bump 로직 없음.
    ///
    /// 파라미터: 없음 (ChaosSkillData.paramsByRarity 는 전 등급 (0,0,0)).
    /// 조회 경로: <see cref="ChaosEffectRegistry"/> 에서 핸들러 획득 후 <see cref="IsActive"/>.
    /// <see cref="ChaosSkillManager.IsGambler"/> 프로퍼티가 해당 조회를 래핑.
    /// </summary>
    public class GamblerHandler : IChaosEffectHandler
    {
        public ChaosEffectType EffectType => ChaosEffectType.Gambler;

        private ChaosHandlerContext ctx;
        private bool subscribed;

        /// <summary>장착 이후 true. 레벨업 선택지 생성부가 파티 전체 순회 시 조회.</summary>
        public bool IsActive { get; private set; }

        public void Apply(ChaosSkillData data, Rarity rolledRarity, ChaosHandlerContext ctx)
        {
            this.ctx = ctx;
            IsActive = true;

            if (!subscribed && ctx.hookBus != null)
            {
                ctx.hookBus.LevelUpChoice += HandleLevelUpChoice;
                subscribed = true;
            }
        }

        public void Remove(ChaosHandlerContext ctx)
        {
            if (subscribed && ctx.hookBus != null)
            {
                ctx.hookBus.LevelUpChoice -= HandleLevelUpChoice;
                subscribed = false;
            }
            IsActive = false;
        }

        private void HandleLevelUpChoice()
        {
            // Rarity bump 로직은 "choice modifier" 추상화 도입 시 여기에 작성.
            // 현재 소비자 없음 — IsActive 만 유효 경로.
        }
    }
}
