using UnityEngine;

namespace SwDreams.Application
{
    /// <summary>
    /// 보스 페이즈 전환 판정. 순수 C# (Unity 의존 최소).
    /// Domain 레이어의 BossPhase enum만 참조.
    ///
    /// BossPhaseManager에서 호출.
    /// 단위 테스트 가능: 체력 비율 → 페이즈 매핑만 검증.
    /// </summary>
    public class BossPhaseService
    {
        /// <summary>
        /// 현재 HP 비율로 페이즈 결정.
        /// </summary>
        public Domain.BossPhase DeterminePhase(int currentHP, int maxHP,
            float phase2Threshold, float phase3Threshold)
        {
            if (maxHP <= 0) return Domain.BossPhase.Phase1;

            float ratio = (float)currentHP / maxHP;

            if (ratio <= phase3Threshold) return Domain.BossPhase.Phase3;
            if (ratio <= phase2Threshold) return Domain.BossPhase.Phase2;
            return Domain.BossPhase.Phase1;
        }

        /// <summary>
        /// 인원수 기반 보스 HP 계산.
        /// </summary>
        public int CalculateScaledHP(int baseHP, int playerCount, float[] multipliers)
        {
            if (multipliers == null || multipliers.Length == 0)
                return baseHP;

            int index = Mathf.Clamp(playerCount - 1, 0, multipliers.Length - 1);
            return Mathf.RoundToInt(baseHP * multipliers[index]);
        }
    }
}