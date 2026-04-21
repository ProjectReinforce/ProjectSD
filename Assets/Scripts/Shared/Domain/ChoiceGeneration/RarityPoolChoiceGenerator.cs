using System;
using System.Collections.Generic;
using SwDreams.Shared.Domain.ValueObjects;

namespace SwDreams.Shared.Domain.ChoiceGeneration
{
    /// <summary>
    /// 4등급 공통 선정기. 순수 C#.
    ///
    /// 규칙: "먼저 Rarity 롤 → 해당 등급 풀에서 count 장 중복 없이 샘플".
    /// 카드 3장이 항상 같은 등급이 되도록 강제.
    ///
    /// 용도: 혼돈 스킬 선택지, 능력치 부스트 선택지, 무기 조합 미리보기 등
    /// "동일 등급 N 장" 규칙이 필요한 모든 UI 플로우에서 공용.
    /// </summary>
    public static class RarityPoolChoiceGenerator
    {
        /// <summary>
        /// pool 에서 count 장을 중복 없이 샘플.
        /// - 먼저 weights 로 Rarity 롤.
        /// - 선택된 등급에 해당하는 pool 원소 중에서 count 장 랜덤 샘플.
        /// - 해당 등급 풀이 비어 있으면 인접 등급(낮은 쪽 우선)으로 폴백.
        /// - 풀 자체가 비어 있으면 빈 배열 반환.
        /// </summary>
        public static T[] PickChoices<T>(
            IEnumerable<T> pool,
            Func<T, Rarity> rarityOf,
            float[] weights,
            int count,
            Random rng)
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            if (rarityOf == null) throw new ArgumentNullException(nameof(rarityOf));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (count <= 0) return Array.Empty<T>();

            List<T> all = new List<T>(pool);
            if (all.Count == 0) return Array.Empty<T>();

            Rarity rolled = RarityWeightedRoller.Roll(weights, rng);
            List<T> filtered = PickByRarityWithFallback(all, rarityOf, rolled);
            if (filtered.Count == 0) return Array.Empty<T>();

            // Fisher-Yates 부분 셔플로 count 장 샘플
            int take = Math.Min(count, filtered.Count);
            for (int i = 0; i < take; i++)
            {
                int j = i + rng.Next(filtered.Count - i);
                (filtered[i], filtered[j]) = (filtered[j], filtered[i]);
            }

            T[] result = new T[take];
            for (int i = 0; i < take; i++) result[i] = filtered[i];
            return result;
        }

        private static List<T> PickByRarityWithFallback<T>(
            List<T> all, Func<T, Rarity> rarityOf, Rarity rolled)
        {
            List<T> filtered = FilterByRarity(all, rarityOf, rolled);
            if (filtered.Count > 0) return filtered;

            // 낮은 등급으로 폴백 (Legendary → Epic → Rare → Common)
            for (int r = (int)rolled - 1; r >= 0; r--)
            {
                filtered = FilterByRarity(all, rarityOf, (Rarity)r);
                if (filtered.Count > 0) return filtered;
            }

            // 높은 등급으로도 폴백
            for (int r = (int)rolled + 1; r < 4; r++)
            {
                filtered = FilterByRarity(all, rarityOf, (Rarity)r);
                if (filtered.Count > 0) return filtered;
            }

            return filtered;
        }

        private static List<T> FilterByRarity<T>(
            List<T> all, Func<T, Rarity> rarityOf, Rarity rarity)
        {
            List<T> result = new List<T>();
            for (int i = 0; i < all.Count; i++)
            {
                if (rarityOf(all[i]) == rarity) result.Add(all[i]);
            }
            return result;
        }
    }
}
