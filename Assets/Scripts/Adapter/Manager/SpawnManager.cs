using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using SwDreams.Application;
using SwDreams.Data;
using SwDreams.Adapter.Entity;

namespace SwDreams.Adapter.Manager
{
    /// <summary>
    /// 적 스폰 관리. Phase 3 고도화 버전.
    /// 
    /// DifficultyManager를 통해 시간대별 난이도 곡선 적용.
    /// 4종 적 타입을 비율에 따라 스폰.
    /// 
    /// 동기화 방식:
    /// - 평소: 호스트가 RPC_SpawnEnemy를 RpcTarget.All로 전송
    /// - 중도 참가: 호스트가 OnPlayerEnteredRoom에서 현재 활성 적 목록 전송
    /// - 사망: 호스트가 RPC_EnemyDied를 RpcTarget.All로 전송
    /// - 강제 제거: 호스트가 RPC_EnemyRemoved를 RpcTarget.All로 전송
    /// 
    /// 셋업:
    /// - GameScene에 빈 GameObject → SpawnManager + PhotonView 부착
    /// - enemyPrefab, 4개 EnemyData SO, DifficultyData SO 인스펙터에서 연결
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class SpawnManager : MonoBehaviourPunCallbacks
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

        [Header("난이도")]
        [SerializeField] private DifficultyData difficultyData;

        [Header("Swarm 설정")]
        [SerializeField] private float swarmLifetime = 8f;

        [Header("시작 대기")]
        [SerializeField] private float startDelay = 2f;

        // 서비스
        private DifficultyManager difficulty;
        private DamageService damageService = new DamageService();
        private string currentPhaseName = "";

        // 적 추적
        private Dictionary<int, Enemy> activeEnemies = new();
        private int nextEnemyId = 0;

        private float spawnTimer;
        private bool isReady = false;
        private float startDelayTimer = -1f;

        // EnemyType → EnemyData 매핑
        private Dictionary<EnemyType, EnemyData> enemyDataMap;

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
            // DifficultyManager 초기화
            difficulty = new DifficultyManager(difficultyData);

            // EnemyData 매핑
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
                int prewarmCount = 30;
                if (difficultyData.spawnPhases.Length > 0)
                    prewarmCount = difficultyData.spawnPhases[difficultyData.spawnPhases.Length - 1].maxEnemyCount;

                PoolManager.Instance?.Prewarm(enemyPrefab, prewarmCount);
            }
            if (orbPrefab != null)
                PoolManager.Instance?.Prewarm(orbPrefab, 50);
        }

        private void Update()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

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
                    Debug.Log("[SpawnManager] 준비 완료. 스폰 시작.");
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

                if (activeEnemies.Count < maxEnemies)
                {
                    SpawnWave(gameTime, playerCount, maxEnemies);
                }

                spawnTimer = difficulty.GetSpawnInterval(gameTime);
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
                if (activeEnemies.Count >= maxEnemies) break;

                EnemyType type = difficulty.GetRandomEnemyType(gameTime);

                if (type == EnemyType.Swarm)
                {
                    SpawnSwarmGroup(hpMultiplier, maxEnemies);
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
                if (activeEnemies.Count >= maxEnemies) break;

                int id = nextEnemyId++;
                photonView.RPC(nameof(RPC_SpawnSwarm), RpcTarget.All,
                    id, groupPos, hpMultiplier, baseAngle);
            }
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
                    int typeInt = (int)enemy.EnemyType;

                    if (enemy.EnemyType == EnemyType.Swarm)
                    {
                        // Swarm은 위치만 동기화 (이미 이동 중이라 방향은 달라질 수 있음)
                        photonView.RPC(nameof(RPC_SpawnSwarm), newPlayer,
                            enemy.EnemyId, (Vector2)enemy.transform.position, 1f, 0f);
                    }
                    else
                    {
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
        private void RPC_EnemyDied(int enemyId, Vector2 deathPosition, int expValue)
        {
            if (!activeEnemies.TryGetValue(enemyId, out Enemy enemy)) return;

            activeEnemies.Remove(enemyId);
            PoolManager.Instance.Return(enemy.gameObject);

            SpawnExpOrb(deathPosition, expValue);

            Debug.Log($"[SpawnManager] 적 사망 ID:{enemyId}, 남은: {activeEnemies.Count}");
        }

        [PunRPC]
        private void RPC_EnemyRemoved(int enemyId)
        {
            if (!activeEnemies.TryGetValue(enemyId, out Enemy enemy)) return;

            activeEnemies.Remove(enemyId);
            PoolManager.Instance.Return(enemy.gameObject);
        }

        private void SpawnExpOrb(Vector2 position, int expValue)
        {
            if (orbPrefab == null) return;

            int playerCount = PhotonNetwork.CurrentRoom?.PlayerCount ?? 1;
            float expMul = difficulty.GetExpMultiplier(playerCount);
            int adjustedExp = Mathf.RoundToInt(expValue * expMul);
            if (adjustedExp < 1) adjustedExp = 1;

            GameObject obj = PoolManager.Instance.Get(orbPrefab);
            var orb = obj.GetComponent<ExperienceOrb>();
            orb?.Initialize(position, adjustedExp);
        }

        // ===== 호스트 전용 이벤트 핸들러 =====

        private void OnEnemyDied(Enemy enemy)
        {
            enemy.OnDiedWithRef -= OnEnemyDied;
            enemy.OnForceReturned -= OnEnemyForceReturned;

            photonView.RPC(nameof(RPC_EnemyDied), RpcTarget.All,
                enemy.EnemyId, (Vector2)enemy.transform.position, enemy.ExpValue);
        }

        private void OnEnemyForceReturned(Enemy enemy)
        {
            enemy.OnDiedWithRef -= OnEnemyDied;
            enemy.OnForceReturned -= OnEnemyForceReturned;

            photonView.RPC(nameof(RPC_EnemyRemoved), RpcTarget.All, enemy.EnemyId);
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
        /// 플레이어 centroid 기준 spawnMin~Max 거리 + 전체 플레이어 safeZone 회피.
        /// </summary>
        private Vector2 GetSpawnPosition()
        {
            Vector2 center = GetPlayerCentroid();
            float minDist = difficulty.SpawnMinDistance;
            float maxDist = difficulty.SpawnMaxDistance;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = Random.Range(minDist, maxDist);
                Vector2 candidate = center + new Vector2(
                    Mathf.Cos(angle) * distance,
                    Mathf.Sin(angle) * distance);

                if (IsPositionSafe(candidate))
                    return candidate;
            }

            // 10번 실패 시 그냥 최대 거리에 스폰
            float fallbackAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            return center + new Vector2(
                Mathf.Cos(fallbackAngle) * maxDist,
                Mathf.Sin(fallbackAngle) * maxDist);
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

        public int ActiveEnemyCount => activeEnemies.Count;

        private void OnDrawGizmosSelected()
        {
            if (difficultyData == null) return;

            Vector3 center = Camera.main != null
                ? Camera.main.transform.position
                : transform.position;
            center.z = 0;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(center, difficultyData.spawnMinDistance);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(center, difficultyData.spawnMaxDistance);
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(center, difficultyData.playerSafeZone);
        }
    }
}