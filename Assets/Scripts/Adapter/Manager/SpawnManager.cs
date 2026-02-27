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
    /// 적 스폰 관리.
    /// 
    /// 동기화 방식:
    /// - 평소: 호스트가 RPC_SpawnEnemy를 RpcTarget.All로 전송
    /// - 중도 참가: 호스트가 OnPlayerEnteredRoom에서 현재 활성 적 목록을 새 플레이어에게 전송
    /// - 사망: 호스트가 RPC_EnemyDied를 RpcTarget.All로 전송
    /// 
    /// 셋업:
    /// - GameScene에 빈 GameObject → SpawnManager + PhotonView 부착
    /// - enemyPrefab, chaserData 인스펙터에서 연결
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class SpawnManager : MonoBehaviourPunCallbacks
    {
        public static SpawnManager Instance { get; private set; }

        [Header("스폰 설정")]
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private GameObject orbPrefab;
        [SerializeField] private EnemyData chaserData;
        [SerializeField] private float spawnInterval = 2.5f;
        [SerializeField] private int maxEnemies = 30;

        [Header("스폰 범위")]
        [SerializeField] private float spawnMinDistance = 8f;
        [SerializeField] private float spawnMaxDistance = 12f;

        [Header("시작 대기")]
        [SerializeField] private float startDelay = 2f;

        // Application 서비스
        private DamageService damageService = new DamageService();

        // 적 추적
        private Dictionary<int, Enemy> activeEnemies = new();
        private int nextEnemyId = 0;

        private float spawnTimer;
        private bool isReady = false;

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
            if (enemyPrefab != null)
                PoolManager.Instance?.Prewarm(enemyPrefab, maxEnemies);
            if (orbPrefab != null)
                PoolManager.Instance?.Prewarm(orbPrefab, 30);

            // 모든 클라이언트 로드 대기
            Invoke(nameof(SetReady), startDelay);
        }

        private void SetReady()
        {
            isReady = true;
            Debug.Log("[SpawnManager] 준비 완료. 스폰 시작.");
        }

        private void Update()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (!isReady) return;
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0 && activeEnemies.Count < maxEnemies)
            {
                Vector2 spawnPos = GetSpawnPosition();
                int id = nextEnemyId++;

                photonView.RPC(nameof(RPC_SpawnEnemy), RpcTarget.All, id, spawnPos);
                spawnTimer = spawnInterval;
            }
        }

        // ===== 중도 참가 처리 =====

        /// <summary>
        /// 새 플레이어가 방에 들어왔을 때 호스트가 현재 적 목록을 전송.
        /// </summary>
        public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            foreach (var kvp in activeEnemies)
            {
                Enemy enemy = kvp.Value;
                if (enemy != null && enemy.IsAlive)
                {
                    photonView.RPC(nameof(RPC_SpawnEnemy), newPlayer,
                        enemy.EnemyId, (Vector2)enemy.transform.position);
                }
            }

            Debug.Log($"[SpawnManager] 새 플레이어에게 활성 적 {activeEnemies.Count}마리 동기화");
        }

        // ===== RPC =====

        [PunRPC]
        private void RPC_SpawnEnemy(int enemyId, Vector2 position)
        {
            // 이미 존재하는 적이면 무시 (중도 참가 시 중복 방지)
            if (activeEnemies.ContainsKey(enemyId)) return;
            if (enemyPrefab == null || chaserData == null) return;

            GameObject obj = PoolManager.Instance.Get(enemyPrefab);
            Enemy enemy = obj.GetComponent<Enemy>();

            if (enemy == null)
            {
                Debug.LogError("[SpawnManager] Enemy 컴포넌트 없음");
                PoolManager.Instance.Return(obj);
                return;
            }

            enemy.Initialize(enemyId, chaserData, position, damageService);
            activeEnemies[enemyId] = enemy;

            // 호스트만 사망 이벤트 구독
            if (PhotonNetwork.IsMasterClient)
            {
                enemy.OnDiedWithRef += OnEnemyDied;
            }
        }

        [PunRPC]
        private void RPC_EnemyDied(int enemyId, Vector2 deathPosition, int expValue)
        {
            if (!activeEnemies.TryGetValue(enemyId, out Enemy enemy)) return;

            activeEnemies.Remove(enemyId);
            PoolManager.Instance.Return(enemy.gameObject);

            // 경험치 오브 스폰 (모든 클라이언트에서 로컬 생성)
            SpawnExpOrb(deathPosition, expValue);

            Debug.Log($"[SpawnManager] 적 사망 ID:{enemyId}, 남은: {activeEnemies.Count}");
        }

        private void SpawnExpOrb(Vector2 position, int expValue)
        {
            if (orbPrefab == null) return;

            GameObject obj = PoolManager.Instance.Get(orbPrefab);
            var orb = obj.GetComponent<ExperienceOrb>();
            orb?.Initialize(position, expValue);
        }

        // ===== 호스트 전용 =====

        private void OnEnemyDied(Enemy enemy)
        {
            enemy.OnDiedWithRef -= OnEnemyDied;

            photonView.RPC(nameof(RPC_EnemyDied), RpcTarget.All,
                enemy.EnemyId, (Vector2)enemy.transform.position, enemy.ExpValue);
        }

        private Vector2 GetSpawnPosition()
        {
            Vector2 center = Vector2.zero;
            var players = GameObject.FindGameObjectsWithTag("Player");
            if (players.Length > 0)
                center = players[0].transform.position;

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(spawnMinDistance, spawnMaxDistance);

            return center + new Vector2(
                Mathf.Cos(angle) * distance,
                Mathf.Sin(angle) * distance
            );
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, spawnMinDistance);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, spawnMaxDistance);
        }
    }
}
