using System.Collections.Generic;

namespace SwDreams.Features.Unlock.Domain
{
    /// <summary>
    /// 메타 진행도 누적 통계 read-only 뷰. UnlockEvaluator 의 입력.
    ///
    /// 구현체는 Adapter 의 MetaProgressStore (자기 PC PlayerPrefs 동기화).
    /// run-statistics.md 의 LocalRunStats 와는 별도 — 본 인터페이스는 영구 누적,
    /// LocalRunStats 는 한 런 휘발성.
    /// </summary>
    public interface IRunStats
    {
        /// <summary>전체 누적 킬 수 (자기 막타).</summary>
        int TotalKills { get; }

        /// <summary>전체 누적 데스 수.</summary>
        int TotalDeaths { get; }

        /// <summary>플레이한 런 수 (클리어/실패 모두 포함).</summary>
        int TotalRuns { get; }

        /// <summary>클리어한 런 수.</summary>
        int TotalClears { get; }

        /// <summary>처치한 보스 ID 셋.</summary>
        IReadOnlyCollection<int> BossDefeatedIds { get; }

        /// <summary>방문한 존 ID 셋.</summary>
        IReadOnlyCollection<int> ZonesVisitedIds { get; }

        /// <summary>나를 죽인 적 ID 셋.</summary>
        IReadOnlyCollection<int> DeathByEnemyIds { get; }
    }
}
