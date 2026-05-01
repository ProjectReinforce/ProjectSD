using UnityEngine;
using SwDreams.Features.Skill.Adapter.Data;
using SwDreams.Features.Boss.Presentation;
using SwDreams.Features.Boss.Adapter.Data;
using SwDreams.Features.Boss.Adapter;
using SwDreams.Shared.Managers;
using Photon.Pun;
using SwDreams.Shared.Data;

namespace SwDreams.Features.Boss.Adapter
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
        [Tooltip("일반 몹 스폰 정책(centroid + 카메라 시야 4면 + offset)에 추가로 더 두는 보스 전용 마진(unit).\n" +
                 "보스 콜라이더가 크므로 시야 가장자리에서 더 멀리 등장시키기 위함.")]
        [SerializeField] private float bossSpawnMargin = 2.5f;

        [Header("맵 경계 가드 (선택)")]
        [Tooltip("맵 외곽 Collider2D. 후보 위치가 이 콜라이더 bounds 안에 들어가면 reject.\n" +
                 "맵 사이즈가 확정되면 맵 외곽 콜라이더를 연결. 비워두면 가드 비활성.")]
        [SerializeField] private Collider2D mapBoundsCollider;

        [Tooltip("맵 외부 가드를 적용할지 여부. mapBoundsCollider 가 연결돼도 false 면 무시.")]
        [SerializeField] private bool enforceOutsideMap = false;

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

                // 보스 혼돈 스킬 적용
                if (BossChaosApplicator.Instance != null)
                    BossChaosApplicator.Instance.ApplyToBoss(currentBoss);

                // 클라이언트에 보스 초기 데이터 동기화
                int bossViewID = bossObj.GetComponent<PhotonView>().ViewID;
                photonView.RPC(nameof(RPC_InitBoss), RpcTarget.Others,
                    bossViewID, currentBoss.MaxHP);
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

                // 보스 혼돈 스킬 적용
                if (BossChaosApplicator.Instance != null)
                    BossChaosApplicator.Instance.ApplyToBoss(currentBoss);

                // 클라이언트에 보스 초기 데이터 동기화
                int bossViewID = bossObj.GetComponent<PhotonView>().ViewID;
                photonView.RPC(nameof(RPC_InitBoss), RpcTarget.Others,
                    bossViewID, currentBoss.MaxHP);
            }

            GameManager.Instance?.ChangeStateNetwork(GameManager.GameState.BossFight);

            Debug.Log($"[BossSpawner] 비상 보스 스폰 (HP배율: {hpMultiplier})");
        }

        // ===== 호스트 마이그레이션 지원 =====

        /// <summary>
        /// 호스트 마이그레이션 시 상태 리셋.
        /// 이전 호스트의 보스 오브젝트가 CleanupCacheOnLeave로 파괴되므로,
        /// 새 호스트가 GameTime 기준으로 보스를 다시 스폰할 수 있도록 플래그 초기화.
        /// HostMigrationHandler에서 호출.
        /// </summary>
        public void ResetForMigration()
        {
            currentBoss = null;
            bossSpawned = false;
            warningStarted = false;
            warningTimer = 0f;
            Debug.Log("[BossSpawner] 마이그레이션 리셋 완료 — GameTime 기준 보스 재트리거 대기");
        }

        /// <summary>
        /// 보스 스폰 위치 결정. 일반 몹 정책(SpawnManager.GetSpawnPosition)과 동일하게
        /// 플레이어 centroid + 카메라 시야 4면(상/하/좌/우) + 보스 전용 마진 위치에서 등장.
        /// 멀티에서 플레이어가 멀리 퍼져 있을 때도 centroid 기준이라 한쪽 플레이어에 치우치지 않음.
        ///
        /// 맵 경계 가드: enforceOutsideMap=true && mapBoundsCollider 연결 시,
        /// 후보 위치가 맵 콜라이더 bounds 안이면 reject 후 다른 면으로 재시도.
        /// 시도 한도 초과 시 fallback(centroid 기준 대각선 + 큰 마진)으로 폴백.
        /// </summary>
        private Vector2 CalculateSpawnPosition()
        {
            Vector2 center = GetPlayerCentroid();
            Camera cam = Camera.main;

            float camHalfH = cam != null ? cam.orthographicSize : 5f;
            float camHalfW = cam != null ? camHalfH * cam.aspect : camHalfH * 1.78f;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector2 candidate = PickSideCandidate(center, camHalfW, camHalfH, bossSpawnMargin);

                // 맵 경계 가드: 콜라이더가 연결되고 enforce 가 켜져 있으면 맵 안 후보는 reject.
                if (enforceOutsideMap && mapBoundsCollider != null &&
                    mapBoundsCollider.bounds.Contains(candidate))
                {
                    continue;
                }

                return candidate;
            }

            // fallback: centroid 에서 대각선 + 큰 마진. 맵 가드는 무시(무한 루프 방지).
            float diagonal = Mathf.Sqrt(camHalfW * camHalfW + camHalfH * camHalfH) + bossSpawnMargin * 2f;
            float fallbackAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            return center + new Vector2(
                Mathf.Cos(fallbackAngle) * diagonal,
                Mathf.Sin(fallbackAngle) * diagonal);
        }

        private static Vector2 PickSideCandidate(Vector2 center, float camHalfW, float camHalfH, float margin)
        {
            int side = Random.Range(0, 4);
            switch (side)
            {
                case 0: // 위
                    return center + new Vector2(
                        Random.Range(-camHalfW, camHalfW),
                        camHalfH + margin);
                case 1: // 아래
                    return center + new Vector2(
                        Random.Range(-camHalfW, camHalfW),
                        -(camHalfH + margin));
                case 2: // 오른쪽
                    return center + new Vector2(
                        camHalfW + margin,
                        Random.Range(-camHalfH, camHalfH));
                default: // 왼쪽
                    return center + new Vector2(
                        -(camHalfW + margin),
                        Random.Range(-camHalfH, camHalfH));
            }
        }

        private static Vector2 GetPlayerCentroid()
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            if (players.Length == 0) return Vector2.zero;

            Vector2 sum = Vector2.zero;
            foreach (var p in players)
                sum += (Vector2)p.transform.position;
            return sum / players.Length;
        }

        // ===== RPC =====

        [PunRPC]
        private void RPC_BossWarning(float duration)
        {
            // Phase 7: 보스 경고 UI 표시
            BossWarningUI.Show(duration,
                BossChaosApplicator.Instance != null
                    ? BossChaosApplicator.Instance.BossChaosType
                    : SwDreams.Features.Skill.Adapter.Data.ChaosEffectType.None);
            Debug.Log($"[BossSpawner] 보스 경고 UI ({duration}초)");
        }

        /// <summary>
        /// 클라이언트에서 보스 초기 상태 설정.
        /// PhotonView.ViewID로 보스 오브젝트를 찾아 HP 초기화 + 참조 캐싱.
        /// </summary>
        [PunRPC]
        private void RPC_InitBoss(int bossViewID, int maxHP)
        {
            PhotonView bossView = PhotonView.Find(bossViewID);
            if (bossView == null)
            {
                Debug.LogWarning($"[BossSpawner] 클라이언트: 보스 ViewID {bossViewID} 찾기 실패");
                return;
            }

            currentBoss = bossView.GetComponent<Boss>();
            if (currentBoss != null)
            {
                currentBoss.InitializeFromNetwork(maxHP);
                bossSpawned = true;
                Debug.Log($"[BossSpawner] 클라이언트: 보스 초기화 완료 (HP:{maxHP})");
            }
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