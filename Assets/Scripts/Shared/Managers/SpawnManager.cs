using System.Collections.Generic;
using SwDreams.Features.Boss.Adapter;
using SwDreams.Features.Progression.Adapter;
using SwDreams.Features.Enemy.Adapter.Data;
using SwDreams.Features.Enemy.Adapter;
using SwDreams.Features.Enemy.Adapter.Attack;
using SwDreams.Features.Pickup.Adapter;
using SwDreams.Features.Skill.Adapter;
using SwDreams.Features.Skill.Application;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using SwDreams.Shared.Managers;
using SwDreams.Shared.Data;

namespace SwDreams.Shared.Managers
{
    /// <summary>
    /// 적 스폰 관리. Phase 3 고도화 버전.
    /// 
    /// DifficultyManager를 통해 시간대별 난이도 곡선 적용.
    /// 4종 적 타입을 비율에 따라 스폰.
    /// 
    /// 동기화 방식:
    /// - 스폰: 호스트가 RPC_SpawnEnemy를 RpcTarget.All로 전송
    /// - 위치: RaiseEvent Unreliable 배치 (Dead Reckoning 보정용)
    /// - 사망: RaiseEvent Reliable 배치 (프레임 당 1회, 대량 동시 사망 최적화)
    /// - 강제 제거: RaiseEvent Reliable 배치
    /// - 데미지 요청: 클라이언트 → 호스트 RPC (C안)
    /// - 중도 참가: 호스트가 OnPlayerEnteredRoom에서 현재 활성 적 목록 전송
    /// 
    /// 셋업:
    /// - GameScene에 빈 GameObject → SpawnManager + PhotonView 부착
    /// - enemyPrefab, 4개 EnemyData SO, DifficultyData SO 인스펙터에서 연결
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class SpawnManager : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        public static SpawnManager Instance { get; private set; }

        [Header("프리팹")]
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private GameObject orbPrefab;

        [Header("적 데이터 (4종)")]
        [SerializeField] private EnemyData chaserData;
        [SerializeField] private EnemyData runnerData;
        [SerializeField] private EnemyData tankData;
        [SerializeField] private EnemyData swarmData;

        // RPC_SpawnRanged / OnPlayerEnteredRoom 이 이 배열의 "인덱스"를 네트워크 식별자로 사용한다.
        // 배열 순서 변경/중간 요소 제거는 리모트 간 variant 불일치를 일으키므로 금지.
        // 필요 시 배열 말미에만 추가하고, 중간 요소는 null 로 비워둘 것.
        [Header("원거리 변형 (고정·추격 × 투사체·경고, 순서 고정 배열)")]
        [SerializeField] private EnemyData[] rangedVariants;

        [Header("원거리 공격 공용 프리팹")]
        [SerializeField] private GameObject enemyProjectilePrefab;
        [SerializeField] private GameObject telegraphPrefab;

        // eliteVariants 인덱스를 RPC 식별자로 사용한다 (Ranged 와 동일 계약).
        // 배열 순서 변경/중간 요소 제거는 리모트 간 variant 불일치를 일으키므로 금지.
        // 운영 규약: 같은 SO 를 rangedVariants 와 eliteVariants 에 동시 등록하지 말 것.
        // (스폰 경로가 이중화되어 밸런싱이 뒤틀림. 엘리트 SO 는 항상 isElite=true 로 표시하고
        //  eliteVariants 에만 등록.)
        [Header("엘리트 변형 (Phase C, 순서 고정 배열)")]
        [SerializeField] private EnemyData[] eliteVariants;

        // questBarrierVariants 인덱스를 RPC 식별자로 사용 (Ranged/Elite 와 동일 계약).
        // QuestData.barrierEnemyData 가 이 배열에 등록된 SO 를 참조해야 인덱스 매칭 가능.
        [Header("퀘스트 격리 몹 변형 (Phase 6, 순서 고정 배열)")]
        [SerializeField] private EnemyData[] questBarrierVariants;
        [Tooltip("엘리트 스폰 간격(초). 일반 스폰과 병행 동작. 0 이하 또는 enableEliteSpawn=false 면 비활성.")]
        [SerializeField] private float eliteSpawnInterval = 90f;
        [SerializeField] private bool enableEliteSpawn = true;

        [Header("난이도")]
        [SerializeField] private DifficultyData difficultyData;

        [Header("Swarm 설정")]
        [SerializeField] private float swarmLifetime = 8f;

        [Header("시작 대기")]
        [SerializeField] private float startDelay = 2f;

        [Header("풀 Prewarm 수량")]
        [SerializeField] private int orbPrewarmCount = 50;
        [SerializeField] private int enemyProjectilePrewarmCount = 30;
        [SerializeField] private int telegraphPrewarmCount = 20;

        // 서비스
        private DifficultyManager difficulty;
        private DamageService damageService = new DamageService();
        private string currentPhaseName = "";

        // 적 추적
        private Dictionary<int, Enemy> activeEnemies = new();
        private int nextEnemyId = 0;

        // 격리 몹 ID 추적 — 일반 maxEnemies 카운트에서 제외하기 위함.
        private readonly HashSet<int> questBarrierIds = new HashSet<int>();

        /// <summary>일반 적 카운트 (격리 몹 제외). maxEnemies 비교 시 사용.</summary>
        private int CountNonBarrierEnemies() => activeEnemies.Count - questBarrierIds.Count;

        private float spawnTimer;
        private float eliteSpawnTimer;
        private bool isReady = false;
        private float startDelayTimer = -1f;

        /// <summary>
        /// R8: 첫 적 스폰 가능 시점 도달 여부. 모든 클라에서 동기화됨 (호스트가 RPC AllBuffered 송신).
        /// SkillExecutor/Skill 의 발동 가드용. true 가 될 때까지 액티브 스킬은 대기.
        /// </summary>
        public bool IsReady => isReady;

        // 적 위치 동기화 (호스트 → 클라이언트)
        [Header("위치 동기화")]
        [SerializeField] private float positionSyncInterval = 0.2f;
        private float positionSyncTimer;

        // EnemyType → EnemyData 매핑
        private Dictionary<EnemyType, EnemyData> enemyDataMap;

        // ===== RaiseEvent 이벤트 코드 =====
        private const byte EventCode_PositionSync = 10;   // Unreliable
        private const byte EventCode_EnemyDeathBatch = 11; // Reliable
        private const byte EventCode_EnemyRemoveBatch = 12; // Reliable

        // ===== 사망/제거 배치 큐 (호스트 전용) =====
        private readonly List<(int enemyId, Vector2 pos, int exp, int killerActorNumber)> deathQueue = new();
        private readonly List<int> removeQueue = new();

        // ===== 활성 경험치 오브 추적 (FIFO, 모든 클라에서 독립적으로 유지) =====
        // 상한 도달 시 가장 오래된 오브의 XP 를 새 오브에 합산 + 반환.
        // 사망 배치는 Reliable 채널로 모든 클라에 동일 순서 수신 → 결정론적 병합.
        private readonly List<ExperienceOrb> activeOrbs = new();

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // DifficultyManager 초기화 — bossSpawnTime을 GameplayConfig에서 가져옴
            float bossTime = GameManager.Instance?.Config != null
                ? GameManager.Instance.Config.bossSpawnTime
                : 900f;
            difficulty = new DifficultyManager(difficultyData, bossTime);

            // EnemyData 매핑 (Ranged 는 variant 기반이라 rangedVariants 배열로 별도 관리)
            enemyDataMap = new Dictionary<EnemyType, EnemyData>
            {
                { EnemyType.Chaser, chaserData },
                { EnemyType.Runner, runnerData },
                { EnemyType.Tank, tankData },
                { EnemyType.Swarm, swarmData }
            };

            // 풀 Prewarm
            if (enemyPrefab != null)
            {
                int prewarmCount = difficultyData.maxEnemyEnd;
                PoolManager.Instance?.Prewarm(enemyPrefab, prewarmCount);
            }
            if (orbPrefab != null)
                PoolManager.Instance?.Prewarm(orbPrefab, orbPrewarmCount);
            if (enemyProjectilePrefab != null)
                PoolManager.Instance?.Prewarm(enemyProjectilePrefab, enemyProjectilePrewarmCount);
            if (telegraphPrefab != null)
                PoolManager.Instance?.Prewarm(telegraphPrefab, telegraphPrewarmCount);
        }

        private void Update()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (GameManager.Instance == null) return;

            var state = GameManager.Instance.CurrentState;

            // 적 위치 동기화: Playing + BossFight에서 동작
            if (state == GameManager.GameState.Playing ||
                state == GameManager.GameState.BossFight)
            {
                UpdatePositionSync();
            }

            // 스폰 로직: Playing에서만 동작
            if (state != GameManager.GameState.Playing) return;

            // Playing 진입 후 딜레이
            if (!isReady)
            {
                if (startDelayTimer < 0f)
                {
                    startDelayTimer = startDelay;
                    Debug.Log($"[SpawnManager] Playing 감지. {startDelay}초 후 스폰 시작.");
                }

                startDelayTimer -= Time.deltaTime;
                if (startDelayTimer <= 0f)
                {
                    isReady = true;
                    // 엘리트 타이머는 "시작 직후 즉시 스폰" 방지를 위해 interval 로 초기화.
                    eliteSpawnTimer = eliteSpawnInterval;
                    Debug.Log("[SpawnManager] 준비 완료. 스폰 시작.");

                    // R8: 모든 클라에 준비 완료 신호 — Skill/SkillExecutor 가 발동 가드 해제.
                    // AllBuffered 로 송신해 후입장 클라(중도 참가)도 자동 수신.
                    photonView.RPC(nameof(RPC_NotifySpawnReady), RpcTarget.AllBuffered);

                    // 엘리트 스폰 진단 로그 (1회)
                    int eliteValid = 0;
                    if (eliteVariants != null)
                        foreach (var v in eliteVariants) if (v != null) eliteValid++;
                    if (enableEliteSpawn)
                        Debug.Log($"[SpawnManager] 엘리트 스폰 활성 — variants 유효={eliteValid}개, interval={eliteSpawnInterval}s");
                    else
                        Debug.Log("[SpawnManager] 엘리트 스폰 비활성 (enableEliteSpawn=false)");
                }
                return;
            }

            float gameTime = GameManager.Instance.GameTime;
            int playerCount = PhotonNetwork.CurrentRoom?.PlayerCount ?? 1;

            // // 보스 시간 도달 시 스폰 중지
            // if (difficulty.IsBossTime(gameTime)) return;

            // Phase 변경 로그
            string phaseName = difficulty.GetCurrentPhaseName(gameTime);
            if (phaseName != currentPhaseName)
            {
                currentPhaseName = phaseName;
                Debug.Log($"[SpawnManager] === Phase 변경: {phaseName} (GameTime: {gameTime:F1}초) ===");
            }

            // 보스 시간 도달 시 스폰 중지
            if (difficulty.IsBossTime(gameTime))
            {
                if (currentPhaseName != "BOSS")
                {
                    currentPhaseName = "BOSS";
                    Debug.Log($"[SpawnManager] === 보스 시간 도달! 스폰 중지. (GameTime: {gameTime:F1}초) ===");
                }
                return;
            }

            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f)
            {
                int maxEnemies = difficulty.GetMaxEnemyCount(gameTime, playerCount);

                if (CountNonBarrierEnemies() < maxEnemies)
                {
                    SpawnWave(gameTime, playerCount, maxEnemies);
                }

                spawnTimer = difficulty.GetSpawnInterval(gameTime);
            }

            // 엘리트 독립 스폰 타이머 — 일반 스폰과 병행
            if (enableEliteSpawn && eliteVariants != null && eliteVariants.Length > 0 && eliteSpawnInterval > 0f)
            {
                eliteSpawnTimer -= Time.deltaTime;
                if (eliteSpawnTimer <= 0f)
                {
                    int maxEnemies = difficulty.GetMaxEnemyCount(gameTime, playerCount);
                    int nonBarrier = CountNonBarrierEnemies();
                    if (nonBarrier < maxEnemies)
                    {
                        float hpMul = difficulty.GetHealthMultiplier(gameTime, playerCount);
                        SpawnElite(hpMul);
                    }
                    else
                    {
                        Debug.Log($"[SpawnManager] 엘리트 스폰 스킵 — 동시 적 상한 도달 ({nonBarrier}/{maxEnemies}). 다음 interval 까지 대기.");
                    }
                    eliteSpawnTimer = eliteSpawnInterval;
                }
            }
        }

        // ===== 스폰 로직 =====

        /// <summary>
        /// 한 틱에 여러 마리 스폰. Swarm이면 그룹으로.
        /// </summary>
        private void SpawnWave(float gameTime, int playerCount, int maxEnemies)
        {
            int spawnCount = difficulty.GetSpawnPerTick(gameTime);
            float hpMultiplier = difficulty.GetHealthMultiplier(gameTime, playerCount);

            for (int i = 0; i < spawnCount; i++)
            {
                if (CountNonBarrierEnemies() >= maxEnemies) break;

                EnemyType type = difficulty.GetRandomEnemyType(gameTime);

                // Ranged 가 롤링됐지만 variant 가 비어 있으면 Chaser 로 폴백
                if (type == EnemyType.Ranged && (rangedVariants == null || rangedVariants.Length == 0))
                    type = EnemyType.Chaser;

                if (type == EnemyType.Swarm)
                {
                    SpawnSwarmGroup(hpMultiplier, maxEnemies);
                }
                else if (type == EnemyType.Ranged)
                {
                    int variantIdx = Random.Range(0, rangedVariants.Length);
                    Vector2 pos = GetSpawnPosition();
                    int id = nextEnemyId++;
                    photonView.RPC(nameof(RPC_SpawnRanged), RpcTarget.All,
                        id, variantIdx, pos, hpMultiplier);
                }
                else
                {
                    Vector2 pos = GetSpawnPosition();
                    int id = nextEnemyId++;
                    int typeInt = (int)type;
                    photonView.RPC(nameof(RPC_SpawnEnemy), RpcTarget.All,
                        id, typeInt, pos, hpMultiplier);
                }
            }
        }

        private void SpawnSwarmGroup(float hpMultiplier, int maxEnemies)
        {
            int groupSize = difficulty.GetSwarmGroupSize();
            Vector2 groupPos = GetSpawnPosition();
            float baseAngle = Random.Range(0f, 360f);

            for (int i = 0; i < groupSize; i++)
            {
                if (CountNonBarrierEnemies() >= maxEnemies) break;

                int id = nextEnemyId++;
                photonView.RPC(nameof(RPC_SpawnSwarm), RpcTarget.All,
                    id, groupPos, hpMultiplier, baseAngle);
            }
        }

        /// <summary>
        /// 일반 적 스폰 중단. BossSpawner에서 보스 등장 시 호출.
        /// </summary>
        public void StopSpawning()
        {
            isReady = false;
            Debug.Log("[SpawnManager] 스폰 중단 (보스 등장)");
        }

        // ===== Phase 6: 퀘스트 격리 몹 =====

        /// <summary>
        /// QuestZone 이 격리 몹을 스폰할 때 호출. 호스트만.
        /// QuestData.barrierEnemyData 를 questBarrierVariants 에서 인덱스로 변환 후 RPC.
        /// 반환: 스폰된 적의 enemyId 배열 (QuestZone 이 보관 후 완료/실패 시 DespawnEnemies 호출).
        /// </summary>
        public int[] SpawnQuestBarriers(EnemyData barrierData, Vector2 center, float radius, int count)
        {
            if (!PhotonNetwork.IsMasterClient) return System.Array.Empty<int>();
            if (barrierData == null || count <= 0) return System.Array.Empty<int>();
            if (questBarrierVariants == null || questBarrierVariants.Length == 0)
            {
                Debug.LogWarning("[SpawnManager] questBarrierVariants 미설정 — 격리 몹 스폰 불가.");
                return System.Array.Empty<int>();
            }

            int variantIdx = System.Array.IndexOf(questBarrierVariants, barrierData);
            if (variantIdx < 0)
            {
                Debug.LogWarning($"[SpawnManager] {barrierData.name} 가 questBarrierVariants 에 등록되지 않음.");
                return System.Array.Empty<int>();
            }

            int[] ids = new int[count];
            for (int i = 0; i < count; i++)
            {
                float angle = (Mathf.PI * 2f * i) / count;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Vector2 pos = center + offset;

                int id = nextEnemyId++;
                ids[i] = id;
                photonView.RPC(nameof(RPC_SpawnQuestBarrier), RpcTarget.All, id, variantIdx, pos);
            }
            return ids;
        }

        /// <summary>지정 enemyId 들을 강제 제거. 퀘스트 종료 시 격리 몹 정리 용도.</summary>
        public void DespawnEnemies(int[] enemyIds)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (enemyIds == null) return;
            for (int i = 0; i < enemyIds.Length; i++)
            {
                if (activeEnemies.TryGetValue(enemyIds[i], out var enemy) && enemy != null)
                    enemy.ForceReturn();
            }
        }

        [PunRPC]
        private void RPC_SpawnQuestBarrier(int enemyId, int variantIdx, Vector2 position)
        {
            if (activeEnemies.ContainsKey(enemyId)) return;
            if (enemyPrefab == null) return;
            if (questBarrierVariants == null || variantIdx < 0 || variantIdx >= questBarrierVariants.Length) return;

            EnemyData data = questBarrierVariants[variantIdx];
            if (data == null) return;

            GameObject obj = PoolManager.Instance.Get(enemyPrefab);
            Enemy enemy = obj.GetComponent<Enemy>();
            if (enemy == null)
            {
                PoolManager.Instance.Return(obj);
                return;
            }

            enemy.Initialize(enemyId, data, position, damageService, hpMultiplier: 1f);

            // 격리 몹은 Kinematic + EnemyMovement 비활성 — 외부 force 도 무시 + movementStrategy 의
            // transform.position 변경도 차단. 가만히 서서 벽 역할만.
            var rb = obj.GetComponent<Rigidbody2D>();
            if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
            var movement = obj.GetComponent<SwDreams.Features.Enemy.Adapter.EnemyMovement>();
            if (movement != null) movement.enabled = false;

            activeEnemies[enemyId] = enemy;
            // 일반 maxEnemies 카운트에서 제외하기 위해 별도 set 에도 등록.
            questBarrierIds.Add(enemyId);

            if (PhotonNetwork.IsMasterClient)
            {
                enemy.OnDiedWithRef += OnEnemyDied;
                enemy.OnForceReturned += OnEnemyForceReturned;
            }
        }

        /// <summary>
        /// 호스트 마이그레이션 시 호출. 현재 활성 적 정리 + 스폰 재개.
        /// 1초 딜레이 후 현재 GameTime 웨이브부터 재개.
        /// HostMigrationHandler에서 호출.
        /// </summary>
        public void ResetForMigration()
        {
            // 활성 적 전부 정리 (이전 호스트의 네트워크 오브젝트는 이미 파괴됨)
            var remainingEnemies = GameObject.FindGameObjectsWithTag("Enemy");
            int cleanedCount = 0;
            foreach (var enemyObj in remainingEnemies)
            {
                if (enemyObj.GetComponent<Boss>() != null) continue;
                enemyObj.SetActive(false);
                cleanedCount++;
            }

            activeEnemies.Clear();
            questBarrierIds.Clear();

            // 잔존 경험치 오브 정리 — 역순 순회로 OnReturnToPool 재진입 안전
            for (int i = activeOrbs.Count - 1; i >= 0; i--)
            {
                var orb = activeOrbs[i];
                if (orb != null && orb.gameObject.activeInHierarchy)
                    PoolManager.Instance?.Return(orb.gameObject);
            }
            activeOrbs.Clear();

            // 잔존 스킬 투사체/이펙트 정리
            CleanupProjectiles();

            // 1초 딜레이 후 스폰 재개
            isReady = false;
            startDelayTimer = 1f;
            spawnTimer = 0f;
            eliteSpawnTimer = eliteSpawnInterval; // 마이그레이션 직후 즉시 엘리트 스폰 방지
            currentPhaseName = "";

            Debug.Log($"[SpawnManager] 마이그레이션 리셋 — 적 {cleanedCount}마리 정리, 1초 후 스폰 재개");
        }

        /// <summary>
        /// 씬에 남아있는 투사체/스킬 이펙트 오브젝트 정리.
        /// 풀에 반환 가능하면 반환, 아니면 비활성화.
        /// </summary>
        private void CleanupProjectiles()
        {
            int count = 0;

            // Projectile 컴포넌트를 가진 모든 오브젝트
            var projectiles = FindObjectsByType<SwDreams.Features.Skill.Adapter.Projectile>(
                FindObjectsSortMode.None);
            foreach (var proj in projectiles)
            {
                if (proj.gameObject.activeInHierarchy)
                {
                    if (PoolManager.Instance != null)
                        PoolManager.Instance.Return(proj.gameObject);
                    else
                        proj.gameObject.SetActive(false);
                    count++;
                }
            }

            // SkillEffect 산하 AreaZone 등 독립 이펙트 (Enemy 태그가 아닌 것만)
            var zones = FindObjectsByType<SwDreams.Features.Skill.Adapter.AreaZone>(
                FindObjectsSortMode.None);
            foreach (var zone in zones)
            {
                if (zone.gameObject.activeInHierarchy)
                {
                    if (PoolManager.Instance != null)
                        PoolManager.Instance.Return(zone.gameObject);
                    else
                        zone.gameObject.SetActive(false);
                    count++;
                }
            }

            if (count > 0)
                Debug.Log($"[SpawnManager] 투사체/이펙트 {count}개 정리");
        }

        // ===== 적 위치 동기화 (호스트 → 클라이언트, Unreliable) =====

        /// <summary>
        /// 호스트가 주기적으로 활성 적 위치를 배치 전송.
        /// Unreliable 채널 사용 — 패킷 손실 시 다음 틱 데이터로 대체.
        /// Dead Reckoning으로 클라이언트가 자체 이동하므로 손실 허용 가능.
        /// </summary>
        private void UpdatePositionSync()
        {
            if (activeEnemies.Count == 0) return;
            if (PhotonNetwork.CurrentRoom.PlayerCount <= 1) return;

            positionSyncTimer += Time.deltaTime;
            if (positionSyncTimer < positionSyncInterval) return;
            positionSyncTimer = 0f;

            // 배치 데이터 구성: [id, posX, posY, id, posX, posY, ...]
            float[] batch = new float[activeEnemies.Count * 3];
            int idx = 0;

            foreach (var kvp in activeEnemies)
            {
                Enemy enemy = kvp.Value;
                if (enemy == null || !enemy.IsAlive) continue;

                batch[idx++] = kvp.Key;
                batch[idx++] = enemy.transform.position.x;
                batch[idx++] = enemy.transform.position.y;
            }

            if (idx < batch.Length)
                System.Array.Resize(ref batch, idx);

            if (batch.Length == 0) return;

            PhotonNetwork.RaiseEvent(
                EventCode_PositionSync,
                batch,
                new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                SendOptions.SendUnreliable);
        }

        // ===== 중도 참가 처리 =====

        public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            foreach (var kvp in activeEnemies)
            {
                Enemy enemy = kvp.Value;
                if (enemy != null && enemy.IsAlive)
                {
                    // 엘리트 우선 판정 — 타입과 무관하게 eliteVariants 역인덱스로 재스폰
                    if (enemy.IsElite)
                    {
                        int idx = eliteVariants != null
                            ? System.Array.IndexOf(eliteVariants, enemy.Data)
                            : -1;
                        if (idx >= 0)
                        {
                            photonView.RPC(nameof(RPC_SpawnElite), newPlayer,
                                enemy.EnemyId, idx, (Vector2)enemy.transform.position, 1f);
                        }
                        else
                        {
                            Debug.LogWarning($"[SpawnManager] 엘리트 중도참가 재스폰 실패 — " +
                                $"enemy.Data({enemy.Data?.name}) 가 eliteVariants 배열에 없음. " +
                                $"인스펙터에서 해당 SO 가 등록됐는지 확인.");
                        }
                    }
                    else if (enemy.EnemyType == EnemyType.Swarm)
                    {
                        // Swarm은 위치만 동기화 (이미 이동 중이라 방향은 달라질 수 있음)
                        photonView.RPC(nameof(RPC_SpawnSwarm), newPlayer,
                            enemy.EnemyId, (Vector2)enemy.transform.position, 1f, 0f);
                    }
                    else if (enemy.EnemyType == EnemyType.Ranged)
                    {
                        int variantIdx = rangedVariants != null
                            ? System.Array.IndexOf(rangedVariants, enemy.Data)
                            : -1;
                        if (variantIdx >= 0)
                        {
                            photonView.RPC(nameof(RPC_SpawnRanged), newPlayer,
                                enemy.EnemyId, variantIdx, (Vector2)enemy.transform.position, 1f);
                        }
                    }
                    else
                    {
                        int typeInt = (int)enemy.EnemyType;
                        photonView.RPC(nameof(RPC_SpawnEnemy), newPlayer,
                            enemy.EnemyId, typeInt, (Vector2)enemy.transform.position, 1f);
                    }
                }
            }

            Debug.Log($"[SpawnManager] 새 플레이어에게 활성 적 {activeEnemies.Count}마리 동기화");
        }

        // ===== RPC =====

        [PunRPC]
        private void RPC_SpawnEnemy(int enemyId, int enemyTypeInt, Vector2 position, float hpMultiplier)
        {
            if (activeEnemies.ContainsKey(enemyId)) return;
            if (enemyPrefab == null) return;

            EnemyType type = (EnemyType)enemyTypeInt;
            EnemyData data = GetEnemyData(type);
            if (data == null)
            {
                Debug.LogWarning($"[SpawnManager] EnemyData 없음: {type}");
                return;
            }

            GameObject obj = PoolManager.Instance.Get(enemyPrefab);
            Enemy enemy = obj.GetComponent<Enemy>();

            if (enemy == null)
            {
                Debug.LogError("[SpawnManager] Enemy 컴포넌트 없음");
                PoolManager.Instance.Return(obj);
                return;
            }

            enemy.Initialize(enemyId, data, position, damageService, hpMultiplier);
            activeEnemies[enemyId] = enemy;

            if (PhotonNetwork.IsMasterClient)
            {
                enemy.OnDiedWithRef += OnEnemyDied;
                enemy.OnForceReturned += OnEnemyForceReturned;
            }
        }

        [PunRPC]
        private void RPC_SpawnSwarm(int enemyId, Vector2 position, float hpMultiplier, float baseAngle)
        {
            if (activeEnemies.ContainsKey(enemyId)) return;
            if (enemyPrefab == null || swarmData == null) return;

            GameObject obj = PoolManager.Instance.Get(enemyPrefab);
            Enemy enemy = obj.GetComponent<Enemy>();

            if (enemy == null)
            {
                PoolManager.Instance.Return(obj);
                return;
            }

            enemy.Initialize(enemyId, swarmData, position, damageService, hpMultiplier);

            var movement = obj.GetComponent<EnemyMovement>();
            movement?.InitializeSwarm(baseAngle, 30f, swarmLifetime);

            activeEnemies[enemyId] = enemy;

            if (PhotonNetwork.IsMasterClient)
            {
                enemy.OnDiedWithRef += OnEnemyDied;
                enemy.OnForceReturned += OnEnemyForceReturned;
            }
        }

        [PunRPC]
        private void RPC_SpawnRanged(int enemyId, int variantIdx, Vector2 position, float hpMultiplier)
        {
            if (activeEnemies.ContainsKey(enemyId)) return;
            if (enemyPrefab == null) return;
            if (rangedVariants == null || variantIdx < 0 || variantIdx >= rangedVariants.Length) return;

            EnemyData data = rangedVariants[variantIdx];
            if (data == null) return;

            GameObject obj = PoolManager.Instance.Get(enemyPrefab);
            Enemy enemy = obj.GetComponent<Enemy>();

            if (enemy == null)
            {
                PoolManager.Instance.Return(obj);
                return;
            }

            enemy.Initialize(enemyId, data, position, damageService, hpMultiplier);
            activeEnemies[enemyId] = enemy;

            if (PhotonNetwork.IsMasterClient)
            {
                enemy.OnDiedWithRef += OnEnemyDied;
                enemy.OnForceReturned += OnEnemyForceReturned;
            }
        }

        // ===== 엘리트 스폰 (Phase C) =====

        private void SpawnElite(float hpMultiplier)
        {
            // 비어있는 슬롯 스킵
            int candidates = 0;
            for (int i = 0; i < eliteVariants.Length; i++)
                if (eliteVariants[i] != null) candidates++;

            if (candidates == 0)
            {
                Debug.LogWarning("[SpawnManager] 엘리트 스폰 시도 실패 — eliteVariants 배열에 유효 SO 가 0 개. " +
                                 "인스펙터에서 Elite Variants 배열에 EnemyData SO 를 드래그했는지 확인.");
                return;
            }

            // 유효 인덱스 중 랜덤 선택
            int pick = Random.Range(0, candidates);
            int eliteIdx = -1;
            for (int i = 0; i < eliteVariants.Length; i++)
            {
                if (eliteVariants[i] == null) continue;
                if (pick == 0) { eliteIdx = i; break; }
                pick--;
            }
            if (eliteIdx < 0) return;

            Vector2 pos = GetSpawnPosition();
            int id = nextEnemyId++;
            Debug.Log($"[SpawnManager] 엘리트 스폰: {eliteVariants[eliteIdx].enemyName} (idx={eliteIdx}, id={id}, pos={pos})");
            photonView.RPC(nameof(RPC_SpawnElite), RpcTarget.All,
                id, eliteIdx, pos, hpMultiplier);
        }

        [PunRPC]
        private void RPC_SpawnElite(int enemyId, int eliteIdx, Vector2 position, float hpMultiplier)
        {
            if (activeEnemies.ContainsKey(enemyId)) return;
            if (enemyPrefab == null) return;
            if (eliteVariants == null || eliteIdx < 0 || eliteIdx >= eliteVariants.Length) return;

            EnemyData data = eliteVariants[eliteIdx];
            if (data == null) return;

            GameObject obj = PoolManager.Instance.Get(enemyPrefab);
            Enemy enemy = obj.GetComponent<Enemy>();

            if (enemy == null)
            {
                PoolManager.Instance.Return(obj);
                return;
            }

            enemy.Initialize(enemyId, data, position, damageService, hpMultiplier);
            activeEnemies[enemyId] = enemy;

            if (PhotonNetwork.IsMasterClient)
            {
                enemy.OnDiedWithRef += OnEnemyDied;
                enemy.OnForceReturned += OnEnemyForceReturned;
            }
        }

        // ===== 원거리 공격 RPC (호스트 → 모든 클라, 로컬 렌더/호스트 판정) =====

        /// <summary>
        /// EnemyAttack(호스트)에서 호출. 투사체를 모든 클라에 스폰.
        /// </summary>
        public void RaiseEnemyProjectile(Vector2 pos, Vector2 dir, float speed, int damage, float lifetime)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            photonView.RPC(nameof(RPC_SpawnEnemyProjectile), RpcTarget.All,
                pos, dir, speed, damage, lifetime);
        }

        /// <summary>
        /// EnemyAttack(호스트)에서 호출. 경고존을 모든 클라에 스폰.
        /// 만료 시 데미지 판정은 호스트만 (TelegraphZone 내부).
        /// </summary>
        public void RaiseTelegraph(Vector2 pos, float duration, float radius, int damage)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            photonView.RPC(nameof(RPC_SpawnTelegraph), RpcTarget.All,
                pos, duration, radius, damage);
        }

        [PunRPC]
        private void RPC_SpawnEnemyProjectile(Vector2 pos, Vector2 dir, float speed, int damage, float lifetime)
        {
            if (enemyProjectilePrefab == null) return;

            GameObject obj = PoolManager.Instance.Get(enemyProjectilePrefab);
            var proj = obj.GetComponent<EnemyProjectile>();
            if (proj == null)
            {
                PoolManager.Instance.Return(obj);
                return;
            }
            proj.Initialize(pos, dir, speed, damage, lifetime);
        }

        [PunRPC]
        private void RPC_SpawnTelegraph(Vector2 pos, float duration, float radius, int damage)
        {
            if (telegraphPrefab == null) return;

            GameObject obj = PoolManager.Instance.Get(telegraphPrefab);
            var zone = obj.GetComponent<TelegraphZone>();
            if (zone == null)
            {
                PoolManager.Instance.Return(obj);
                return;
            }
            zone.Initialize(pos, duration, radius, damage);
        }

        // ===== 사망/제거는 OnEvent에서 배치 처리 — 아래 OnEvent 참조 =====

        // ===== 클라이언트 → 호스트 데미지 요청 (C안) =====

        /// <summary>
        /// 클라이언트에서 호출. 자기 투사체/장판이 적을 맞혔을 때
        /// 호스트에 데미지 처리를 요청.
        /// actorNumber: 데미지를 준 플레이어 (킬러 귀속 효과용).
        /// isCrit: 클라 측에서 굴린 치명타 결과 (호스트 화면 DamagePopup 색상 일치용).
        /// </summary>
        public void RequestDamage(int enemyId, int damage, int actorNumber, bool isCrit = false)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                ApplyDamageOnHost(enemyId, damage, actorNumber, isCrit);
                return;
            }
            photonView.RPC(nameof(RPC_RequestDamage), RpcTarget.MasterClient,
                enemyId, damage, actorNumber, isCrit);
        }

        /// <summary>
        /// 클라이언트에서 호출. 넉백 요청.
        /// </summary>
        public void RequestKnockback(int enemyId, Vector2 sourcePos, float force)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                ApplyKnockbackOnHost(enemyId, sourcePos, force);
                return;
            }
            photonView.RPC(nameof(RPC_RequestKnockback), RpcTarget.MasterClient,
                enemyId, sourcePos, force);
        }

        [PunRPC]
        private void RPC_RequestDamage(int enemyId, int damage, int actorNumber, bool isCrit)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            ApplyDamageOnHost(enemyId, damage, actorNumber, isCrit);
        }

        [PunRPC]
        private void RPC_RequestKnockback(int enemyId, Vector2 sourcePos, float force)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            ApplyKnockbackOnHost(enemyId, sourcePos, force);
        }

        /// <summary>
        /// R8: 호스트가 첫 스폰 가능 시점 도달 시 AllBuffered 로 송신.
        /// 모든 클라(후입장 포함)에서 isReady=true → Skill/SkillExecutor 발동 가드 해제.
        /// </summary>
        [PunRPC]
        private void RPC_NotifySpawnReady()
        {
            if (isReady) return;
            isReady = true;
            Debug.Log("[SpawnManager] 스폰 준비 신호 수신 — 스킬 발동 가드 해제");
        }

        private void ApplyDamageOnHost(int enemyId, int damage, int actorNumber, bool isCrit = false)
        {
            if (!activeEnemies.TryGetValue(enemyId, out Enemy enemy)) return;
            if (enemy == null || !enemy.IsAlive) return;
            enemy.LastDamagerActorNumber = actorNumber;
            enemy.TakeDamage(damage, isCrit);
        }

        private void ApplyKnockbackOnHost(int enemyId, Vector2 sourcePos, float force)
        {
            if (!activeEnemies.TryGetValue(enemyId, out Enemy enemy)) return;
            if (enemy == null || !enemy.IsAlive) return;
            enemy.ApplyKnockback(sourcePos, force);
        }

        private void SpawnExpOrb(Vector2 position, int expValue, int enemyId)
        {
            if (orbPrefab == null) return;

            // 상한 도달 시 새 오브 드랍 생략 (프레임 드랍 방지)
            int maxOrbs = GameManager.Instance?.Config != null
                ? GameManager.Instance.Config.maxActiveExpOrbs
                : 0;
            if (maxOrbs > 0 && activeOrbs.Count >= maxOrbs)
                return;

            int playerCount = PhotonNetwork.CurrentRoom?.PlayerCount ?? 1;
            float gameTime = GameManager.Instance != null ? GameManager.Instance.GameTime : 0f;
            float expMul = difficulty.GetExpMultiplier(gameTime, playerCount);
            int adjustedExp = Mathf.RoundToInt(expValue * expMul);
            if (adjustedExp < 1) adjustedExp = 1;

            // Deterministic scatter — enemyId 시드로 호스트/클라 동일 결과 보장.
            Vector2 scatteredPos = ScatterExpOrbPos(position, enemyId);

            GameObject obj = PoolManager.Instance.Get(orbPrefab);
            var orb = obj.GetComponent<ExperienceOrb>();
            if (orb == null) return;

            orb.Initialize(scatteredPos, adjustedExp);
            activeOrbs.Add(orb);
        }

        /// <summary>
        /// XP 오브 scatter 위치 계산. enemyId 를 seed 로 하는 deterministic RNG —
        /// 각 클라가 독립 실행해도 동일한 결과. 포맷 확장 없이 일관성 유지.
        /// </summary>
        private static Vector2 ScatterExpOrbPos(Vector2 origin, int enemyId)
        {
            float radius = GameManager.Instance?.Config != null
                ? GameManager.Instance.Config.dropScatterRadius
                : 0.5f;
            if (radius <= 0f) return origin;

            var localRng = new System.Random(enemyId);
            float angle = (float)localRng.NextDouble() * Mathf.PI * 2f;
            float r = (float)localRng.NextDouble() * radius;
            return origin + new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
        }

        /// <summary>
        /// ExperienceOrb 가 풀로 반환될 때(획득/병합/마이그레이션 리셋) 호출.
        /// activeOrbs 에서 제거. 이미 제거됐으면 no-op.
        /// </summary>
        public void OnExpOrbReturned(ExperienceOrb orb)
        {
            if (orb == null) return;
            activeOrbs.Remove(orb);
        }

        // ===== 호스트 전용 이벤트 핸들러 =====

        private void OnEnemyDied(Enemy enemy)
        {
            enemy.OnDiedWithRef -= OnEnemyDied;
            enemy.OnForceReturned -= OnEnemyForceReturned;

            // Phase 7: 킬 카운트 추적
            GameStatTracker.Instance?.RecordKill();

            // [Phase 5] 연쇄 폭발 체크 — 킬러만 대상
            NotifyChaosManagers(enemy.transform.position, enemy.LastDamagerActorNumber);

            // 드랍 롤 (정수/무기/자석/물약) — DropTable 규칙에 따라 DropSpawner 가 확률/등급 결정 후 배치.
            // Essence 는 dropTable.essenceChance + enemy.IsElite 둘 다 만족해야 발동.
            if (enemy.Data != null && enemy.Data.dropTable != null)
            {
                DropSpawner.Instance?.TrySpawnDrops(
                    (Vector2)enemy.transform.position,
                    enemy.Data.dropTable,
                    enemy.IsElite);
            }

            // Phase 6: 활성 QuestZone (InProgress) 에 적 처치 통지 — KillTarget/KillInTime 진행률 갱신.
            // 호스트만 호출 (SpawnManager 의 OnEnemyDied 자체가 호스트 핸들러).
            // F7: 격리 몹 자체가 죽으면 KillTarget 카운트에서 제외.
            if (!questBarrierIds.Contains(enemy.EnemyId))
                SwDreams.Features.Quest.Adapter.QuestZone.NotifyEnemyKilledToAllActive();

            // 큐에 적재 → LateUpdate에서 배치 전송 (killerActorNumber 포함)
            deathQueue.Add((enemy.EnemyId, (Vector2)enemy.transform.position,
                enemy.ExpValue, enemy.LastDamagerActorNumber));
        }

        /// <summary>
        /// 킬러 플레이어의 ChaosSkillManager에만 적 사망 알림.
        /// 연쇄 폭발 등 킬러 귀속 효과 트리거.
        /// </summary>
        private void NotifyChaosManagers(Vector3 enemyPosition, int killerActorNumber)
        {
            if (killerActorNumber < 0) return;

            var players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var p in players)
            {
                var pv = p.GetComponent<PhotonView>();
                if (pv == null || pv.Owner == null) continue;
                if (pv.Owner.ActorNumber != killerActorNumber) continue;

                var chaos = p.GetComponentInChildren<SwDreams.Features.Skill.Adapter.ChaosSkillManager>();
                if (chaos != null)
                    chaos.OnEnemyKilled(enemyPosition);
            }
        }

        /// <summary>
        /// 클라이언트용: 킬러 플레이어의 연쇄폭발 비주얼만 재생.
        /// OnReceiveDeathBatch에서 호출.
        /// </summary>
        private void NotifyChaosManagersVisualOnly(Vector2 deathPosition, int killerActorNumber)
        {
            if (killerActorNumber < 0) return;

            var players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var p in players)
            {
                var pv = p.GetComponent<PhotonView>();
                if (pv == null || pv.Owner == null) continue;
                if (pv.Owner.ActorNumber != killerActorNumber) continue;

                var chaos = p.GetComponentInChildren<SwDreams.Features.Skill.Adapter.ChaosSkillManager>();
                if (chaos != null)
                    chaos.OnEnemyKilledVisualOnly(deathPosition);
            }
        }

        private void OnEnemyForceReturned(Enemy enemy)
        {
            enemy.OnDiedWithRef -= OnEnemyDied;
            enemy.OnForceReturned -= OnEnemyForceReturned;

            // 즉시 RPC 대신 큐에 적재 → LateUpdate에서 배치 전송
            removeQueue.Add(enemy.EnemyId);
        }

        // ===== 사망/제거 배치 전송 (프레임 당 1회) =====

        private void LateUpdate()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            FlushDeathQueue();
            FlushRemoveQueue();
        }

        private void FlushDeathQueue()
        {
            if (deathQueue.Count == 0) return;

            // [enemyId, posX, posY, exp, killerActorNumber, ...]
            float[] batch = new float[deathQueue.Count * 5];
            for (int i = 0; i < deathQueue.Count; i++)
            {
                var d = deathQueue[i];
                batch[i * 5]     = d.enemyId;
                batch[i * 5 + 1] = d.pos.x;
                batch[i * 5 + 2] = d.pos.y;
                batch[i * 5 + 3] = d.exp;
                batch[i * 5 + 4] = d.killerActorNumber;
            }

            PhotonNetwork.RaiseEvent(
                EventCode_EnemyDeathBatch,
                batch,
                new RaiseEventOptions { Receivers = ReceiverGroup.All },
                SendOptions.SendReliable);

            deathQueue.Clear();
        }

        private void FlushRemoveQueue()
        {
            if (removeQueue.Count == 0) return;

            int[] batch = removeQueue.ToArray();

            PhotonNetwork.RaiseEvent(
                EventCode_EnemyRemoveBatch,
                batch,
                new RaiseEventOptions { Receivers = ReceiverGroup.All },
                SendOptions.SendReliable);

            removeQueue.Clear();
        }

        // ===== RaiseEvent 수신 (IOnEventCallback) =====

        public void OnEvent(EventData photonEvent)
        {
            switch (photonEvent.Code)
            {
                case EventCode_PositionSync:
                    OnReceivePositionSync((float[])photonEvent.CustomData);
                    break;

                case EventCode_EnemyDeathBatch:
                    OnReceiveDeathBatch((float[])photonEvent.CustomData);
                    break;

                case EventCode_EnemyRemoveBatch:
                    OnReceiveRemoveBatch((int[])photonEvent.CustomData);
                    break;
            }
        }

        private void OnReceivePositionSync(float[] batch)
        {
            for (int i = 0; i + 2 < batch.Length; i += 3)
            {
                int enemyId = (int)batch[i];
                float x = batch[i + 1];
                float y = batch[i + 2];

                if (activeEnemies.TryGetValue(enemyId, out Enemy enemy))
                {
                    if (enemy != null && enemy.IsAlive)
                    {
                        var movement = enemy.GetComponent<EnemyMovement>();
                        if (movement != null)
                            movement.SetNetworkPosition(new Vector2(x, y));
                    }
                }
            }
        }

        private void OnReceiveDeathBatch(float[] batch)
        {
            for (int i = 0; i + 4 < batch.Length; i += 5)
            {
                int enemyId = (int)batch[i];
                Vector2 deathPos = new Vector2(batch[i + 1], batch[i + 2]);
                int expValue = (int)batch[i + 3];
                int killerActorNumber = (int)batch[i + 4];

                if (!activeEnemies.TryGetValue(enemyId, out Enemy enemy)) continue;

                activeEnemies.Remove(enemyId);
                questBarrierIds.Remove(enemyId);
                PoolManager.Instance.Return(enemy.gameObject);

                SpawnExpOrb(deathPos, expValue, enemyId);

                if (IsNearLocalPlayer(deathPos, 15f))
                    GameAudioConnector.Instance?.OnEnemyDied();

                // 연쇄폭발 비주얼 — 킬러만 (클라이언트 전용, 호스트는 OnEnemyDied에서 처리)
                if (!PhotonNetwork.IsMasterClient)
                    NotifyChaosManagersVisualOnly(deathPos, killerActorNumber);
            }
        }

        private void OnReceiveRemoveBatch(int[] batch)
        {
            for (int i = 0; i < batch.Length; i++)
            {
                int enemyId = batch[i];
                if (!activeEnemies.TryGetValue(enemyId, out Enemy enemy)) continue;

                activeEnemies.Remove(enemyId);
                questBarrierIds.Remove(enemyId);
                PoolManager.Instance.Return(enemy.gameObject);
            }
        }

        // ===== 유틸리티 =====

        private EnemyData GetEnemyData(EnemyType type)
        {
            if (enemyDataMap != null && enemyDataMap.TryGetValue(type, out var data))
                return data;
            return chaserData;
        }

        /// <summary>
        /// 스폰 위치 결정.
        /// 카메라 시야 밖 테두리에서 스폰. offset 범위 내 랜덤.
        /// </summary>
        private Vector2 GetSpawnPosition()
        {
            Vector2 center = GetPlayerCentroid();
            Camera cam = Camera.main;

            // 카메라 반크기 계산
            float camHalfH = cam != null ? cam.orthographicSize : 5f;
            float camHalfW = cam != null ? camHalfH * cam.aspect : camHalfH * 1.78f;

            float offsetMin = difficulty.SpawnOffsetMin;
            float offsetMax = difficulty.SpawnOffsetMax;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                float offset = Random.Range(offsetMin, offsetMax);
                Vector2 candidate;

                // 4방향 중 랜덤 (상/하/좌/우)
                int side = Random.Range(0, 4);
                switch (side)
                {
                    case 0: // 위
                        candidate = center + new Vector2(
                            Random.Range(-camHalfW, camHalfW),
                            camHalfH + offset);
                        break;
                    case 1: // 아래
                        candidate = center + new Vector2(
                            Random.Range(-camHalfW, camHalfW),
                            -(camHalfH + offset));
                        break;
                    case 2: // 오른쪽
                        candidate = center + new Vector2(
                            camHalfW + offset,
                            Random.Range(-camHalfH, camHalfH));
                        break;
                    default: // 왼쪽
                        candidate = center + new Vector2(
                            -(camHalfW + offset),
                            Random.Range(-camHalfH, camHalfH));
                        break;
                }

                if (IsPositionSafe(candidate))
                    return candidate;
            }

            // fallback: 랜덤 각도, 카메라 대각선 + maxOffset 거리
            float diagonal = Mathf.Sqrt(camHalfW * camHalfW + camHalfH * camHalfH) + offsetMax;
            float fallbackAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            return center + new Vector2(
                Mathf.Cos(fallbackAngle) * diagonal,
                Mathf.Sin(fallbackAngle) * diagonal);
        }

        /// <summary>
        /// 모든 플레이어로부터 safeZone 이상 떨어져 있는지 확인.
        /// </summary>
        private bool IsPositionSafe(Vector2 position)
        {
            float safeZone = difficulty.PlayerSafeZone;
            var players = GameObject.FindGameObjectsWithTag("Player");

            foreach (var player in players)
            {
                if (Vector2.Distance(position, player.transform.position) < safeZone)
                    return false;
            }

            return true;
        }

        private Vector2 GetPlayerCentroid()
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            if (players.Length == 0) return Vector2.zero;

            Vector2 sum = Vector2.zero;
            foreach (var p in players)
                sum += (Vector2)p.transform.position;

            return sum / players.Length;
        }

        // ===== 디버그 =====

        /// <summary>
        /// 로컬 플레이어와 지정 위치 사이의 거리가 범위 내인지 확인.
        /// 사운드 재생 등 로컬 판단용.
        /// </summary>
        private bool IsNearLocalPlayer(Vector2 position, float maxDistance)
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var p in players)
            {
                var pv = p.GetComponent<PhotonView>();
                if (pv != null && pv.IsMine)
                    return Vector2.Distance(position, p.transform.position) <= maxDistance;
            }
            return false;
        }

        public int ActiveEnemyCount => activeEnemies.Count;

        private void OnDrawGizmosSelected()
        {
            if (difficultyData == null) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 center = cam.transform.position;
            center.z = 0;

            // 카메라 시야 영역
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(center, new Vector3(halfW * 2, halfH * 2, 0));

            // 스폰 최소 오프셋 (화면 밖)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(center, new Vector3(
                (halfW + difficultyData.spawnOffsetMin) * 2,
                (halfH + difficultyData.spawnOffsetMin) * 2, 0));

            // 스폰 최대 오프셋
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(center, new Vector3(
                (halfW + difficultyData.spawnOffsetMax) * 2,
                (halfH + difficultyData.spawnOffsetMax) * 2, 0));

            // 세이프존
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(center, difficultyData.playerSafeZone);
        }
    }
}