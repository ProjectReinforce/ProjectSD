using SwDreams.Shared.Domain.ValueObjects;

namespace SwDreams.Features.Skill.Adapter.Chaos
{
    /// <summary>
    /// 도박꾼(Gambler) 혼돈 효과 — 레벨업 선택지 등급 상승 분포 적용기.
    ///
    /// 효과 (설계 docs/game-design/skills/chaos/gambler.md):
    ///   매 레벨업 시 선택지 카드 전체가 상위 등급으로 등장. 파티원 전체 적용.
    ///   장착자가 한 명이라도 있으면 LevelUpManager 가 모든 플레이어의 rolledRarity 에 본 bump 적용.
    ///
    /// 분포표 (가정: "N단계" = "+N 등급 상승", Legendary 초과 클램프):
    ///   Common    → 100% +1                (→ Rare)
    ///   Rare      →  90% +1 / 10% +2       (→ Epic / Legendary)
    ///   Epic      →  80% +1 / 20% +2       (→ Legendary / Legendary)
    ///   Legendary →  70% +1 / 20% +2 / 10% +3  (모두 Legendary 로 클램프)
    ///
    /// 호출자가 결정 후 별도 RarityWeightedRoller 호출 없이 본 bump 만 적용.
    /// </summary>
    public static class GamblerRarityBumper
    {
        /// <summary>
        /// 입력 등급에 분포표 기반 bump 적용. <paramref name="rng"/> null 시 +1 단계 고정 (테스트 안전성).
        /// </summary>
        public static Rarity Bump(Rarity input, System.Random rng)
        {
            int bump = RollBumpSteps(input, rng);
            int target = (int)input + bump;
            // Rarity enum 은 Common(0) ~ Legendary(3) — 3 으로 클램프.
            if (target > (int)Rarity.Legendary) target = (int)Rarity.Legendary;
            if (target < 0) target = 0;
            return (Rarity)target;
        }

        private static int RollBumpSteps(Rarity input, System.Random rng)
        {
            // 0~99 균등 정수. 누적 임계로 단계 결정.
            int roll = rng != null ? rng.Next(100) : 0;

            switch (input)
            {
                case Rarity.Common:
                    return 1; // 100% +1
                case Rarity.Rare:
                    return roll < 90 ? 1 : 2;
                case Rarity.Epic:
                    return roll < 80 ? 1 : 2;
                case Rarity.Legendary:
                    if (roll < 70) return 1;
                    if (roll < 90) return 2;
                    return 3;
                default:
                    return 1;
            }
        }
    }
}
