using System;

namespace SwDreams.Shared.Domain.ValueObjects
{
    /// <summary>
    /// 가중치 기반 등급 롤 유틸. 순수 C#.
    ///
    /// weights 배열은 Rarity enum 순서(Common/Rare/Epic/Legendary)를 따른다.
    /// 배열 길이가 enum 길이보다 작으면 빈 자리는 0 가중치로 취급.
    /// </summary>
    public static class RarityWeightedRoller
    {
        /// <summary>
        /// 가중치에 비례해 Rarity 하나를 롤.
        /// 총합이 0 이거나 음수면 Common 반환.
        /// </summary>
        public static Rarity Roll(float[] weights, Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (weights == null || weights.Length == 0) return Rarity.Common;

            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                float w = weights[i];
                if (w > 0f) total += w;
            }
            if (total <= 0f) return Rarity.Common;

            float pick = (float)rng.NextDouble() * total;
            float acc = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                float w = weights[i];
                if (w <= 0f) continue;
                acc += w;
                if (pick <= acc) return (Rarity)i;
            }

            return Rarity.Common;
        }
    }
}
