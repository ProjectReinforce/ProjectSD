using System.Collections.Generic;
using UnityEngine;
using SwDreams.Features.Unlock.Domain;
using SwDreams.Shared.Domain.Events;
using SwDreams.Shared.Platform.Adapter;
using SwDreams.Shared.Platform.Domain;

namespace SwDreams.Features.Unlock.Adapter
{
    /// <summary>
    /// 메타 진행도 영구 누적 (meta-unlock.md §11, B-1a 분산 추적).
    ///
    /// - 자기 PC 1 인스턴스 (DontDestroyOnLoad).
    /// - RunEventBus 구독으로 Stats Feature 와 디커플링 (Shared/Domain/Events).
    /// - Awake 에서 PlayerPrefs 로드 → 메모리 캐시.
    /// - RunEnded 시 Save (1 런당 1회 — 매 누적 호출의 PlayerPrefs.Save 부담 회피).
    ///
    /// IRunStats 인터페이스 구현 → Unit 2 의 UnlockEvaluator 입력.
    /// </summary>
    public class MetaProgressStore : MonoBehaviour, IRunStats
    {
        public static MetaProgressStore Instance { get; private set; }

        // ===== 누적 상태 (SerializeField 로 인스펙터 노출 — 게임 중 실시간 값 확인용) =====
        [Header("누적 통계 (Read-only, 인스펙터 디버그용)")]
        [SerializeField] private int totalKills;
        [SerializeField] private int totalDeaths;
        [SerializeField] private int totalRuns;
        [SerializeField] private int totalClears;

        // HashSet 은 Unity 직렬화 미지원 → List mirror 로 인스펙터 노출.
        // 누적 호출 직후 SyncMirrors() 로 동기화. 메모리 SSOT 는 HashSet.
        [SerializeField] private List<int> bossDefeatedIdsMirror = new List<int>();
        [SerializeField] private List<int> zonesVisitedIdsMirror = new List<int>();
        [SerializeField] private List<int> deathByEnemyIdsMirror = new List<int>();

        private readonly HashSet<int> bossDefeatedIds = new HashSet<int>();
        private readonly HashSet<int> zonesVisitedIds = new HashSet<int>();
        private readonly HashSet<int> deathByEnemyIds = new HashSet<int>();

        // ===== IRunStats =====
        public int TotalKills => totalKills;
        public int TotalDeaths => totalDeaths;
        public int TotalRuns => totalRuns;
        public int TotalClears => totalClears;
        public IReadOnlyCollection<int> BossDefeatedIds => bossDefeatedIds;
        public IReadOnlyCollection<int> ZonesVisitedIds => zonesVisitedIds;
        public IReadOnlyCollection<int> DeathByEnemyIds => deathByEnemyIds;

        /// <summary>변동분이 있어 다음 RunEnded 시 Save 해야 하는지.</summary>
        private bool dirty;

        /// <summary>
        /// Awake 시점이 늦어 첫 hook 누락 가능성을 줄이기 위한 lazy 자동 생성.
        /// 호출자(Bootstrap, GameManager, 또는 첫 RunEvent 발화 직전)가 Ensure() 호출.
        /// </summary>
        public static MetaProgressStore GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject(nameof(MetaProgressStore));
            return go.AddComponent<MetaProgressStore>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadFromStorage();

            var bus = RunEventBus.Instance;
            bus.KillRecorded += OnKillRecorded;
            bus.BossDefeatRecorded += OnBossDefeatRecorded;
            bus.DeathRecorded += OnDeathRecorded;
            bus.ZoneVisited += OnZoneVisitedRecorded;
            bus.RunEnded += OnRunEnded;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            var bus = RunEventBus.Instance;
            bus.KillRecorded -= OnKillRecorded;
            bus.BossDefeatRecorded -= OnBossDefeatRecorded;
            bus.DeathRecorded -= OnDeathRecorded;
            bus.ZoneVisited -= OnZoneVisitedRecorded;
            bus.RunEnded -= OnRunEnded;
            Instance = null;
        }

        // ===== 이벤트 핸들러 =====

        private void OnKillRecorded(int sourceSkillId, int enemyId)
        {
            totalKills++;
            dirty = true;
        }

        private void OnBossDefeatRecorded(int bossId)
        {
            // 보스 처치는 totalKills 에도 +1 (LocalStatsRecorder 의 보스 처치 = Kills++ 와 일관).
            totalKills++;
            bossDefeatedIds.Add(bossId);
            SyncMirrors();
            dirty = true;
        }

        private void OnDeathRecorded(int attackerEnemyId)
        {
            totalDeaths++;
            if (attackerEnemyId > 0)
            {
                deathByEnemyIds.Add(attackerEnemyId);
                SyncMirrors();
            }
            dirty = true;
        }

        private void OnZoneVisitedRecorded(int zoneId)
        {
            if (zonesVisitedIds.Add(zoneId))
            {
                SyncMirrors();
                dirty = true;
            }
        }

        /// <summary>HashSet → List mirror 동기화 (인스펙터 노출용).</summary>
        private void SyncMirrors()
        {
            bossDefeatedIdsMirror.Clear();
            bossDefeatedIdsMirror.AddRange(bossDefeatedIds);
            zonesVisitedIdsMirror.Clear();
            zonesVisitedIdsMirror.AddRange(zonesVisitedIds);
            deathByEnemyIdsMirror.Clear();
            deathByEnemyIdsMirror.AddRange(deathByEnemyIds);
        }

        private void OnRunEnded(bool isCleared)
        {
            totalRuns++;
            if (isCleared) totalClears++;
            dirty = true;

            SaveToStorage();

            // SDK 통계 누적 (PlatformBootstrap 미존재 시 NRE 회피).
            var svc = PlatformBootstrap.Service;
            if (svc != null)
            {
                svc.IncrementStat(AchievementId.Stat_TotalRuns, 1);
                if (isCleared) svc.IncrementStat(AchievementId.Stat_TotalClears, 1);
            }
        }

        // ===== 영구 저장 / 로드 =====

        private void LoadFromStorage()
        {
            // PlatformBootstrap 미존재 시 lazy 자동 생성 시도.
            if (PlatformBootstrap.Instance == null)
                PlatformBootstrap.GetOrCreate();

            var dto = RunRecordRepository.LoadStats();
            if (dto == null)
            {
                Debug.Log("[MetaProgress] 저장된 누적 통계 없음 — 신규 시작");
                return;
            }

            totalKills    = dto.totalKills;
            totalDeaths   = dto.totalDeaths;
            totalRuns     = dto.totalRuns;
            totalClears   = dto.totalClears;

            bossDefeatedIds.Clear();
            if (dto.bossDefeatedIds != null)
                foreach (var id in dto.bossDefeatedIds) bossDefeatedIds.Add(id);

            zonesVisitedIds.Clear();
            if (dto.zonesVisitedIds != null)
                foreach (var id in dto.zonesVisitedIds) zonesVisitedIds.Add(id);

            deathByEnemyIds.Clear();
            if (dto.deathByEnemyIds != null)
                foreach (var id in dto.deathByEnemyIds) deathByEnemyIds.Add(id);

            SyncMirrors();
            Debug.Log($"[MetaProgress] Loaded: kills={totalKills}, runs={totalRuns}, clears={totalClears}");
        }

        public void SaveToStorage()
        {
            if (!dirty) return;
            var dto = new RunStatsDto
            {
                totalKills = totalKills,
                totalDeaths = totalDeaths,
                totalRuns = totalRuns,
                totalClears = totalClears,
                bossDefeatedIds = ToArray(bossDefeatedIds),
                zonesVisitedIds = ToArray(zonesVisitedIds),
                deathByEnemyIds = ToArray(deathByEnemyIds),
            };
            RunRecordRepository.SaveStats(dto);
            dirty = false;
            Debug.Log($"[MetaProgress] Saved: kills={totalKills}, runs={totalRuns}, clears={totalClears}");
        }

        private static int[] ToArray(HashSet<int> set)
        {
            var arr = new int[set.Count];
            int i = 0;
            foreach (var v in set) arr[i++] = v;
            return arr;
        }

        /// <summary>테스트 / 디버그 — 누적 진행도 강제 리셋. PlayerPrefs 저장.</summary>
        [ContextMenu("Debug: Reset Meta Progress")]
        public void DebugReset()
        {
            totalKills = totalDeaths = totalRuns = totalClears = 0;
            bossDefeatedIds.Clear();
            zonesVisitedIds.Clear();
            deathByEnemyIds.Clear();
            SyncMirrors();
            dirty = true;
            SaveToStorage();
            Debug.Log("[MetaProgress] DebugReset 완료 — 모든 누적 0 으로 리셋됨");
        }

        /// <summary>인스펙터 우클릭 → 콘솔에 현재 누적 dump.</summary>
        [ContextMenu("Debug: Print Meta Progress")]
        public void DebugPrint()
        {
            Debug.Log($"[MetaProgress] kills={totalKills}, deaths={totalDeaths}, " +
                      $"runs={totalRuns}, clears={totalClears}, " +
                      $"bosses=[{string.Join(",", bossDefeatedIds)}], " +
                      $"zones=[{string.Join(",", zonesVisitedIds)}], " +
                      $"deathBy=[{string.Join(",", deathByEnemyIds)}]");
        }

        /// <summary>인스펙터 우클릭 → 즉시 PlayerPrefs flush (RunEnded 기다리지 않고).</summary>
        [ContextMenu("Debug: Force Save Now")]
        public void DebugForceSave()
        {
            dirty = true;
            SaveToStorage();
        }
    }
}
