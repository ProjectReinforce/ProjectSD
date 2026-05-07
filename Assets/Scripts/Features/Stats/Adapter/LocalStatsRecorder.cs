using UnityEngine;
using SwDreams.Features.Stats.Domain;

namespace SwDreams.Features.Stats.Adapter
{
    /// <summary>
    /// 한 클라이언트의 인-런 통계 누적기 (B-1a — run-statistics.md §5).
    /// 자기 PC 만 자기 통계 누적 — 호스트 마이그레이션 무관.
    ///
    /// 진입점:
    /// - DealDamage*Handler 자기 ActorNumber 매칭 시 → OnFire / AddDamage
    /// - SpawnManager FlushDeathQueue 핸들러 (자기 막타 일반 적) → OnKill(skillId, enemyId)
    /// - Boss.RPC_BossDied (모든 파티원) → OnBossDefeat(bossId)
    /// - PlayerHealth.RPC_TakeDamage 자기 viewId → AddDamageTaken
    /// - PlayerHealth.OnDied 자기 → OnDeath(enemyId)
    ///
    /// 결과 시점: ResultManager.SendLocalBuildToHost 가 Snapshot() 호출해 RPC 첨부.
    /// </summary>
    public class LocalStatsRecorder : MonoBehaviour
    {
        public static LocalStatsRecorder Instance { get; private set; }

        private LocalRunStats stats = new LocalRunStats();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // DontDestroyOnLoad 안 함 — GameScene 진입 시마다 새 인스턴스로 자동 초기화.
            // (게임 종료 → 메뉴 → 새 게임 시 stats 클린 상태 보장)
        }

        /// <summary>
        /// GameScene 에 LocalStatsRecorder 인스턴스가 없으면 자동 생성.
        /// SpawnManager / Boss / PlayerHealth 등 호출자가 LocalStatsRecorder.Instance 를
        /// 게임 시작 직후 참조할 수 있어 lazy 자동 생성으로 timing 안전.
        /// </summary>
        public static LocalStatsRecorder GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject(nameof(LocalStatsRecorder));
            return go.AddComponent<LocalStatsRecorder>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ===== 발사 / 가해 데미지 (자기 발사 시점 자기 PC 누적) =====

        public void OnFire(int sourceSkillId)
        {
            if (sourceSkillId <= 0) return;
            stats.GetOrCreate(sourceSkillId).FireCount++;
        }

        public void AddDamage(int sourceSkillId, float damage)
        {
            if (damage <= 0f) return;
            stats.DamageDealt += damage;
            if (sourceSkillId > 0)
                stats.GetOrCreate(sourceSkillId).DamageDealt += damage;
        }

        // ===== 막타 킬 (사망 RPC 핸들러 진입점) =====

        /// <summary>일반 적 자기 막타 — SpawnManager.FlushDeathQueue 핸들러에서 호출.</summary>
        public void OnKill(int sourceSkillId, int enemyId)
        {
            stats.Kills++;
            if (sourceSkillId > 0)
                stats.GetOrCreate(sourceSkillId).KillCount++;
        }

        /// <summary>보스 처치 — RPC_BossDied 진입점, 모든 파티원 무조건 호출 (D13).</summary>
        public void OnBossDefeat(int bossId)
        {
            // run-statistics: 보스 처치는 모든 파티원에게 +1 표시 (가해자 매칭 안 함).
            // meta-unlock 의 BossDefeatedIds 셋 갱신은 향후 메타 진행도 도입 시 여기서 hook.
            stats.Kills++;
        }

        // ===== 받은 데미지 / 사망 =====

        public void AddDamageTaken(float damage)
        {
            if (damage <= 0f) return;
            stats.DamageTaken += damage;
        }

        public void OnDeath(int attackerEnemyId)
        {
            stats.Deaths++;
            // attackerEnemyId 는 향후 메타 진행도 DeathByEnemy 조건 진입점.
        }

        // ===== Snapshot / Reset =====

        /// <summary>결과 시점 SendLocalBuildToHost 가 호출. 현재 통계 dict 반환.</summary>
        public LocalRunStats Snapshot() => stats;

        /// <summary>새 런 시작 시 초기화. GameManager.OnStateChanged(Playing) 진입 hook.</summary>
        public void ResetForNewRun()
        {
            stats = new LocalRunStats();
        }
    }
}
