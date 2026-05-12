using System.Linq;

namespace SwDreams.Features.Unlock.Domain
{
    /// <summary>
    /// 메타 언락 조건 평가 — 순수 함수.
    /// Adapter 의 UnlockTracker 가 런 종료 시 일괄 호출 (D11).
    /// </summary>
    public static class UnlockEvaluator
    {
        /// <summary>
        /// 단일 조건 평가. 통계 read-only 뷰만 보면 됨.
        /// 새 UnlockConditionType 추가 시 본 switch 분기에 케이스 추가 필수.
        /// </summary>
        public static bool Evaluate(UnlockCondition c, IRunStats stats)
        {
            if (stats == null) return false;
            switch (c.type)
            {
                case UnlockConditionType.None:
                    return true;
                case UnlockConditionType.KillCount:
                    return stats.TotalKills >= c.targetValue;
                case UnlockConditionType.RunsCleared:
                    return stats.TotalClears >= c.targetValue;
                case UnlockConditionType.BossDefeat:
                    return stats.BossDefeatedIds != null && stats.BossDefeatedIds.Contains(c.targetIdA);
                case UnlockConditionType.ZoneVisited:
                    return stats.ZonesVisitedIds != null && stats.ZonesVisitedIds.Contains(c.targetIdA);
                case UnlockConditionType.DeathByEnemy:
                    return stats.DeathByEnemyIds != null && stats.DeathByEnemyIds.Contains(c.targetIdA);
                default:
                    return false;
            }
        }

        /// <summary>
        /// 조건 리스트 AND 평가. 비어있으면 true (= 처음부터 해금).
        /// SkillData/WeaponData/CharacterData 의 unlockConditions 일괄 평가 진입점.
        /// </summary>
        public static bool EvaluateAll(System.Collections.Generic.IList<UnlockCondition> conditions, IRunStats stats)
        {
            if (conditions == null || conditions.Count == 0) return true;
            for (int i = 0; i < conditions.Count; i++)
            {
                if (!Evaluate(conditions[i], stats)) return false;
            }
            return true;
        }
    }
}
