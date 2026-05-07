using System.Collections.Generic;

namespace SwDreams.Features.Stats.Domain
{
    /// <summary>
    /// 한 클라이언트의 인-런 통계 스냅샷 (B-1a — run-statistics.md §3).
    /// LocalStatsRecorder 가 보유. 결과 시점 PlayerBuildData 로 첨부.
    ///
    /// 분산 추적 모델: 각 클라가 자기 ActorNumber 매칭만 누적.
    /// 호스트 마이그레이션 무관 — 자기 PC 데이터 보존.
    /// </summary>
    public class LocalRunStats
    {
        /// <summary>자기 막타 카운트 (일반 적 + 보스).</summary>
        public int Kills;

        /// <summary>자기 사망 횟수.</summary>
        public int Deaths;

        /// <summary>자기가 가한 누적 데미지 (자기 발사 시점 누적, 작은 오차 감수).</summary>
        public float DamageDealt;

        /// <summary>자기가 받은 누적 데미지 (PlayerHealth.RPC_TakeDamage 자기 viewId 매칭).</summary>
        public float DamageTaken;

        /// <summary>스킬 ID → 스킬별 통계.</summary>
        public Dictionary<int, SkillRunStats> BySkill = new Dictionary<int, SkillRunStats>();

        /// <summary>해당 스킬 통계 가져오기 (없으면 신규).</summary>
        public SkillRunStats GetOrCreate(int skillId)
        {
            if (!BySkill.TryGetValue(skillId, out var s))
            {
                s = new SkillRunStats();
                BySkill[skillId] = s;
            }
            return s;
        }
    }
}
