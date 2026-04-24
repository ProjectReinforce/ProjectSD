using System;
using System.Collections.Generic;
using SwDreams.Features.StatBoost.Adapter.Data;
using SwDreams.Shared.Domain.ValueObjects;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.StatBoost.Adapter
{
    /// <summary>
    /// StatBoost 선택지 생성 (통합 등급 방식).
    ///
    /// 흐름:
    /// 1. Rarity 롤 (weights 기반)
    /// 2. DB 전체 SO 에서 count 장 중복 없이 랜덤 샘플
    /// 3. 각 SO 는 내부 valueByRarity[rolled] 로 해석됨
    ///
    /// 기존 RarityPoolChoiceGenerator 와 다른 점:
    /// - 각 SO 가 특정 등급 소속이 아니라 **모든 등급 공용** → 등급 풀 필터링 불필요.
    /// - 같은 세 장 카드가 rolledRarity 를 공유 (StatBoostManager.ApplyChoice 에서 동일 rarity 로 적용).
    ///
    /// 반환: (choices, rolledRarity). 풀 비면 (빈 배열, Common).
    /// </summary>
    public static class StatBoostChoiceService
    {
        public static (StatBoostData[] choices, Rarity rolledRarity) GenerateChoices(
            StatBoostDatabase db,
            int count,
            Random rng,
            float[] rarityWeights = null)
        {
            if (db == null || db.All == null || db.All.Count == 0)
                return (Array.Empty<StatBoostData>(), Rarity.Common);

            if (count <= 0)
                return (Array.Empty<StatBoostData>(), Rarity.Common);

            // 등급 롤 (기본 가중치는 GameplayConfig 공용).
            float[] weights = rarityWeights;
            if (weights == null || weights.Length == 0)
            {
                var cfg = GameManager.Instance?.Config;
                weights = (cfg != null && cfg.defaultRarityWeights != null && cfg.defaultRarityWeights.Length > 0)
                    ? cfg.defaultRarityWeights
                    : new float[] { 60f, 25f, 12f, 3f };
            }
            Rarity rolled = RarityWeightedRoller.Roll(weights, rng);

            // 전체 DB 에서 count 장 중복 없이 샘플 — SO 가 모든 등급을 커버하므로 필터 불필요.
            List<StatBoostData> filtered = new List<StatBoostData>(db.All.Count);
            for (int i = 0; i < db.All.Count; i++)
            {
                var b = db.All[i];
                if (b != null) filtered.Add(b);
            }

            int take = Math.Min(count, filtered.Count);
            // Fisher-Yates 부분 셔플
            for (int i = 0; i < take; i++)
            {
                int j = i + rng.Next(filtered.Count - i);
                (filtered[i], filtered[j]) = (filtered[j], filtered[i]);
            }

            StatBoostData[] result = new StatBoostData[take];
            for (int i = 0; i < take; i++) result[i] = filtered[i];
            return (result, rolled);
        }
    }
}
