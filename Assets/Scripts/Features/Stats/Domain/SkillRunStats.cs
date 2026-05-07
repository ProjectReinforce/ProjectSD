namespace SwDreams.Features.Stats.Domain
{
    /// <summary>
    /// 한 스킬의 인-런 통계 (B-1a — run-statistics.md §3).
    /// 자기 PC 의 LocalStatsRecorder 가 skillId → SkillRunStats dict 로 보관.
    /// 휘발성 — 결과 화면 표시 후 폐기.
    /// </summary>
    public class SkillRunStats
    {
        /// <summary>발사 횟수 (Skill.Fire 시 +1).</summary>
        public int FireCount;

        /// <summary>이 스킬로 막타 친 적 수 (사망 RPC 진입점에서 +1).</summary>
        public int KillCount;

        /// <summary>이 스킬이 가한 누적 데미지 (자기 발사 시점 누적, B-1a 작은 오차 감수).</summary>
        public float DamageDealt;
    }
}
