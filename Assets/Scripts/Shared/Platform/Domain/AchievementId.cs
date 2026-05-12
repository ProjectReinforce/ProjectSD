namespace SwDreams.Shared.Platform.Domain
{
    /// <summary>
    /// 실적 / 통계 ID 상수. Stove/Steam 양쪽에 동일 ID로 매핑.
    /// 실제 SDK 등록 시 이 문자열을 그대로 사용 (Phase B/C).
    ///
    /// 메타 언락 영구 저장 키는 platform-integration.md §10 컨벤션을 별도로 따름
    /// (meta.run_stats / meta.unlocked_*) — 본 클래스는 SDK 표준 ID 만 다룸.
    /// </summary>
    public static class AchievementId
    {
        // ===== 실적 (Achievement) =====

        // 클리어
        public const string FirstClear         = "FIRST_CLEAR";
        public const string ClearWithoutDeath  = "CLEAR_NO_DEATH";

        // 보스
        public const string BossKilled         = "BOSS_KILLED";
        public const string BossKilledChaos    = "BOSS_KILLED_WITH_CHAOS";

        // 진화
        public const string FirstEvolution     = "FIRST_EVOLUTION";
        public const string AllEvolutions      = "ALL_EVOLUTIONS_DISCOVERED";

        // 시간 마일스톤
        public const string Survive10Min       = "SURVIVE_10_MIN";
        public const string Survive15Min       = "SURVIVE_15_MIN";

        // 킬 마일스톤
        public const string Kills_1000         = "TOTAL_KILLS_1000";

        // ===== 통계 ID (IncrementStat 용) =====
        public const string Stat_TotalKills    = "stat_total_kills";
        public const string Stat_TotalDeaths   = "stat_total_deaths";
        public const string Stat_TotalRuns     = "stat_total_runs";
        public const string Stat_TotalClears   = "stat_total_clears";
    }
}
