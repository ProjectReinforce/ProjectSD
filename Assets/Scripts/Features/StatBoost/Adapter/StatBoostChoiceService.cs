using System;
using SwDreams.Features.StatBoost.Adapter.Data;
using SwDreams.Shared.Domain.ChoiceGeneration;
using SwDreams.Shared.Domain.ValueObjects;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.StatBoost.Adapter
{
    /// <summary>
    /// StatBoost 선택지 생성. 호스트 LevelUpManager 가 "스킬 풀 고갈(= 만렙)" 분기에서 호출.
    ///
    /// Phase 0 의 <see cref="RarityPoolChoiceGenerator"/> 를 재사용해
    /// 카드 3 장이 항상 같은 등급이 되도록 보장한다.
    ///
    /// 순수 Adapter 유틸 (상태 없음) — static 메서드로 제공.
    /// </summary>
    public static class StatBoostChoiceService
    {
        /// <summary>
        /// 등급 가중치 + DB 풀 → N 장 선택지.
        /// 풀이 비면 빈 배열.
        /// </summary>
        public static StatBoostData[] GenerateChoices(
            StatBoostDatabase db,
            int count,
            Random rng,
            float[] rarityWeights = null)
        {
            if (db == null || db.All == null || db.All.Count == 0) return Array.Empty<StatBoostData>();
            if (count <= 0) return Array.Empty<StatBoostData>();

            // 등급 가중치 기본값: GameplayConfig.defaultRarityWeights (Common/Rare/Epic/Legendary).
            float[] weights = rarityWeights;
            if (weights == null || weights.Length == 0)
            {
                var cfg = GameManager.Instance?.Config;
                weights = (cfg != null && cfg.defaultRarityWeights != null && cfg.defaultRarityWeights.Length > 0)
                    ? cfg.defaultRarityWeights
                    : new float[] { 60f, 25f, 12f, 3f };
            }

            return RarityPoolChoiceGenerator.PickChoices(
                db.All,
                b => b.rarity,
                weights,
                count,
                rng);
        }
    }
}
