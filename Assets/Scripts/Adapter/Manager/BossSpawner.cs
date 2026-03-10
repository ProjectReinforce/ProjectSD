using UnityEngine;
using Photon.Pun;
using SwDreams.Data;
using SwDreams.Adapter.Entity;

namespace SwDreams.Adapter.Manager
{
    /// <summary>
    /// 보스 스폰 전담. SpawnManager에서 분리 (SRP).
    ///
    /// 플로우:
    /// 1. GameManager.GameTime >= bossSpawnTime 감지 (호스트)
    /// 2. 일반 적 스폰 중단 (SpawnManager.StopSpawning)
    /// 3. 보스 등장 경고 UI (RPC)
    /// 4. 딜레이 후 보스 스폰
    /// 5. GameState → BossFight 전환
    ///
    /// 셋업: GameScene에 빈 GameObject "BossSpawner"
    ///        → BossSpawner + PhotonView 부착
    ///        → bossData 인스펙터에서 BossData SO 연결
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class BossSpawner : MonoBehaviourPun
    {
        public static BossSpawner Instance { get; private set; }

        [Header("데이터")]
        [SerializeField] private BossData bossData;

        [Header("보스 프리팹")]
        [SerializeField] private GameObject bossPrefab;

        [Header("스폰 위치")]
        [SerializeField] private float spawnDistance = 10f; // 플레이어로부터의 거리

        // 상태
        private bool bossSpawned = false;
        private bool warningStarted = false;
        private float warningTimer;

        // 현재 보스 참조
        private Boss currentBoss;
        public Boss CurrentBoss => currentBoss;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (bossSpawned) return;
            if (GameManager.Instance == null) return;
            if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

            float bossTime = GameManager.Instance.Config != null
                ? GameManager.Instance.Config.bossSpawnTime
                : 600f;

            // 보스 등장 시간 도달
            if (GameManager.Instance.GameTime >= bossTime && !warningStarted)
            {
                StartBossWarning();
            }

            // 경고 연출 중
            if (warningStarted)
            {
                warningTimer -= Time.deltaTime;
                if (warningTimer <= 0f)
                {
                    SpawnBoss();
                }
            }
        }

        // ===== 보스 등장 경고 =====

        private void StartBossWarning()
        {
            warningStarted = true;

            float warningDuration = GameManager.Instance.Config != null
                ? GameManager.Instance.Config.bossWarningDuration
                : 3f;
            warningTimer = warningDuration;

            // 일반 적 스폰 중단
            if (SpawnManager.Instance != null)
                SpawnManager.Instance.StopSpawning();

            // 모든 클라이언트에 경고 UI
            photonView.RPC(nameof(RPC_BossWarning), RpcTarget.All, warningDuration);

            Debug.Log($"[BossSpawner] 보스 경고 시작 ({warningDuration}초)");
        }

        // ===== 보스 스폰 =====

        private void SpawnBoss()
        {
            bossSpawned = true;

            // 스폰 위치: 맵 중앙 또는 플레이어 밀집 지역의 반대편
            Vector2 spawnPos = CalculateSpawnPosition();

            // PhotonNetwork로 보스 생성 (모든 클라이언트에서 생성)
            GameObject bossObj = PhotonNetwork.Instantiate(
                bossPrefab.name, spawnPos, Quaternion.identity);

            currentBoss = bossObj.GetComponent<Boss>();
            if (currentBoss != null)
            {
                int playerCount = PhotonNetwork.PlayerList.Length;
                currentBoss.Initialize(bossData, playerCount);

                // BossPhaseManager에 전투 시작 알림
                if (BossPhaseManager.Instance != null)
                    BossPhaseManager.Instance.StartBossFight(currentBoss, bossData);
            }

            // 상태 전환
            GameManager.Instance?.ChangeStateNetwork(GameManager.GameState.BossFight);

            Debug.Log($"[BossSpawner] 보스 스폰 완료: {spawnPos}");
        }

        /// <summary>
        /// 비상 보스전용 즉시 스폰. EmergencyBossHandler에서 호출.
        /// </summary>
        public void SpawnEmergencyBoss(float hpMultiplier = 1f)
        {
            if (bossSpawned) return;
            bossSpawned = true;

            Vector2 spawnPos = CalculateSpawnPosition();

            GameObject bossObj = PhotonNetwork.Instantiate(
                bossPrefab.name, spawnPos, Quaternion.identity);

            currentBoss = bossObj.GetComponent<Boss>();
            if (currentBoss != null)
            {
                int playerCount = PhotonNetwork.PlayerList.Length;
                currentBoss.Initialize(bossData, playerCount);

                // 비상 보스전 약화
                if (hpMultiplier < 1f)
                    currentBoss.ApplyHPMultiplier(hpMultiplier);

                if (BossPhaseManager.Instance != null)
                    BossPhaseManager.Instance.StartBossFight(currentBoss, bossData);
            }

            GameManager.Instance?.ChangeStateNetwork(GameManager.GameState.BossFight);

            Debug.Log($"[BossSpawner] 비상 보스 스폰 (HP배율: {hpMultiplier})");
        }

        private Vector2 CalculateSpawnPosition()
        {
            // 간단 구현: 플레이어 평균 위치에서 spawnDistance만큼 떨어진 랜덤 방향
            var players = GameObject.FindGameObjectsWithTag("Player");
            Vector2 center = Vector2.zero;

            if (players.Length > 0)
            {
                foreach (var p in players)
                    center += (Vector2)p.transform.position;
                center /= players.Length;
            }

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnDistance;
        }

        // ===== RPC =====

        [PunRPC]
        private void RPC_BossWarning(float duration)
        {
            // TODO: UI 연출 — 화면 흔들림 + "보스 등장!" 텍스트 + 혼돈 스킬 아이콘
            Debug.Log($"[BossSpawner] 보스 경고 UI ({duration}초)");
        }

        // ===== 디버그 =====

        /// <summary>
        /// 디버그 키(B)로 즉시 소환. TestManager에서 호출.
        /// </summary>
        public void DebugSpawnBoss()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (bossSpawned) return;

            if (SpawnManager.Instance != null)
                SpawnManager.Instance.StopSpawning();

            SpawnBoss();
        }
    }
}