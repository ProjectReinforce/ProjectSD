namespace SwDreams.Features.Unlock.Domain
{
    /// <summary>
    /// 메타 언락 조건 종류 (meta-unlock.md §5).
    /// 새 조건 추가 시 UnlockEvaluator.Evaluate switch 분기에도 같이 추가.
    /// </summary>
    public enum UnlockConditionType
    {
        /// <summary>조건 없음 — 처음부터 해금된 상태로 취급.</summary>
        None = 0,

        /// <summary>누적 킬 수가 targetValue 이상.</summary>
        KillCount,

        /// <summary>targetIdA = bossId 를 처치한 적 있음.</summary>
        BossDefeat,

        /// <summary>누적 클리어 수가 targetValue 이상.</summary>
        RunsCleared,

        /// <summary>targetIdA = zoneId 에 방문한 적 있음.</summary>
        ZoneVisited,

        /// <summary>targetIdA = enemyId 에게 죽은 적 있음.</summary>
        DeathByEnemy,
    }
}
