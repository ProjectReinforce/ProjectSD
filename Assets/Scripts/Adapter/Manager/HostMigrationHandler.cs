using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using SwDreams.Data;
using SwDreams.Adapter.Entity;

namespace SwDreams.Adapter.Manager
{
    /// <summary>
    /// 호스트 이탈 감지 + 재연결 대기 + 비상 보스전 트리거.
    ///
    /// Photon PUN2의 자동 MasterClient 전환 활용:
    /// - OnMasterClientSwitched: 호스트 변경 감지
    /// - OnPlayerLeftRoom: 플레이어 퇴장 감지
    ///
    /// 플로우:
    /// 1. 호스트 퇴장 감지 (OnPlayerLeftRoom에서 이전 호스트인지 확인)
    /// 2. 게임 일시정지 + ReconnectUI 표시
    /// 3. reconnectWaitTime 초 대기
    /// 4. 새 MasterClient가 비상 보스전 시작
    ///
    /// 셋업: GameScene에 빈 GameObject "HostMigrationHandler"
    ///        → HostMigrationHandler 부착 (PhotonView 불필요 — 콜백만 사용)
    /// </summary>
    public class HostMigrationHandler : MonoBehaviourPunCallbacks
    {
        public static HostMigrationHandler Instance { get; private set; }

        // 상태
        private bool isMigrating = false;
        private int previousMasterActorNumber = -1;

        // 이벤트 (UI용)
        public event System.Action<float, float> OnReconnectTimerUpdated; // remaining, total
        public event System.Action OnMigrationStarted;
        public event System.Action OnMigrationCompleted;

        private float ReconnectWaitTime
        {
            get
            {
                var cfg = GameManager.Instance?.Config;
                return cfg != null ? cfg.reconnectWaitTime : 5f;
            }
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // 현재 호스트 기록
            if (PhotonNetwork.IsConnected && PhotonNetwork.MasterClient != null)
                previousMasterActorNumber = PhotonNetwork.MasterClient.ActorNumber;
        }

        // ===== Photon 콜백 =====

        /// <summary>
        /// Photon 자동 호스트 전환 시 호출.
        /// OnPlayerLeftRoom보다 먼저 호출되므로 여기서 감지.
        /// </summary>
        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            int oldMaster = previousMasterActorNumber;
            previousMasterActorNumber = newMasterClient.ActorNumber;

            Debug.Log($"[HostMigration] 호스트 전환: Actor {oldMaster} → {newMasterClient.NickName} (Actor: {newMasterClient.ActorNumber})");

            // 인게임이 아니면 무시
            if (GameManager.Instance == null) return;
            var state = GameManager.Instance.CurrentState;
            if (state != GameManager.GameState.Playing &&
                state != GameManager.GameState.BossFight &&
                state != GameManager.GameState.Paused)
                return;

            // 내가 새 호스트가 되었으면 마이그레이션 시작
            if (newMasterClient.IsLocal && oldMaster != newMasterClient.ActorNumber)
            {
                Debug.Log("[HostMigration] 내가 새 호스트 — 마이그레이션 시작");
                StartCoroutine(HandleHostMigration());
            }
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            if (GameManager.Instance == null) return;
            var state = GameManager.Instance.CurrentState;
            if (state != GameManager.GameState.Playing &&
                state != GameManager.GameState.BossFight &&
                state != GameManager.GameState.Paused)
                return;

            // 일반 플레이어 퇴장 처리 (호스트 퇴장은 OnMasterClientSwitched에서 처리됨)
            Debug.Log($"[HostMigration] 플레이어 퇴장: {otherPlayer.NickName}");
            HandlePlayerDisconnect(otherPlayer);
        }

        // ===== 호스트 이탈 처리 =====

        /// <summary>
        /// 레벨업 종료 후 비상 보스전을 시작해야 하는지 플래그.
        /// LevelUpManager.EndLevelUpSequence()에서 확인.
        /// </summary>
        public bool PendingEmergencyBoss { get; private set; } = false;

        private IEnumerator HandleHostMigration()
        {
            if (isMigrating) yield break;
            isMigrating = true;

            OnMigrationStarted?.Invoke();

            bool wasInLevelUp = GameManager.Instance != null &&
                                GameManager.Instance.CurrentState == GameManager.GameState.Paused;

            if (wasInLevelUp)
            {
                // 레벨업 중: 5초 대기 없이 즉시 세션 인수
                // 플레이어는 선택 계속, 완료 후 비상 보스전 시작
                Debug.Log("[HostMigration] 레벨업 중 호스트 이탈 — 세션 인수");

                if (PhotonNetwork.IsMasterClient)
                {
                    AdoptOrphanedEnemies();

                    if (LevelUpManager.Instance != null)
                        LevelUpManager.Instance.AdoptLevelUpSession();

                    PendingEmergencyBoss = true;
                }

                isMigrating = false;
                OnMigrationCompleted?.Invoke();
            }
            else
            {
                // 일반 상태(Playing/BossFight): 5초 대기 후 비상 보스전
                Time.timeScale = 0f;
                Debug.Log($"[HostMigration] 재연결 대기 시작 ({ReconnectWaitTime}초)");

                float elapsed = 0f;
                float waitTime = ReconnectWaitTime;

                while (elapsed < waitTime)
                {
                    elapsed += Time.unscaledDeltaTime;
                    OnReconnectTimerUpdated?.Invoke(waitTime - elapsed, waitTime);
                    yield return null;
                }

                Time.timeScale = 1f;
                isMigrating = false;
                OnMigrationCompleted?.Invoke();

                if (PhotonNetwork.IsMasterClient)
                {
                    AdoptOrphanedEnemies();
                    StartEmergencyBoss();
                }
            }
        }

        /// <summary>
        /// LevelUpManager.EndLevelUpSequence()에서 호출.
        /// 레벨업 완료 후 대기 중이던 비상 보스전 시작.
        /// </summary>
        public void OnLevelUpCompleted()
        {
            if (!PendingEmergencyBoss) return;
            PendingEmergencyBoss = false;

            Debug.Log("[HostMigration] 레벨업 완료 → 비상 보스전 시작");
            StartEmergencyBoss();
        }

        private void StartEmergencyBoss()
        {
            // 이미 보스전이면 스킵
            if (GameManager.Instance.CurrentState == GameManager.GameState.BossFight)
            {
                Debug.Log("[HostMigration] 이미 보스전 중 — 비상 보스전 스킵");
                return;
            }

            // 일반 적 스폰 중단
            if (SpawnManager.Instance != null)
                SpawnManager.Instance.StopSpawning();

            // 비상 보스전 HP 약화: 게임 시간이 bossSpawnTime의 70% 이전이면 약화
            float bossTime = GameManager.Instance.Config?.bossSpawnTime ?? 600f;
            float emergencyRatio = GameManager.Instance.Config?.emergencyBossHPRatio ?? 0.7f;
            float timeRatio = GameManager.Instance.GameTime / bossTime;
            float hpMul = timeRatio < emergencyRatio ? timeRatio : 1f;

            if (BossSpawner.Instance != null)
            {
                BossSpawner.Instance.SpawnEmergencyBoss(hpMul);
                Debug.Log($"[HostMigration] 비상 보스 스폰 (시간비율: {timeRatio:F2}, HP배율: {hpMul:F2})");
            }
            else
            {
                Debug.LogError("[HostMigration] BossSpawner 없음!");
                // 폴백: 그냥 게임 종료
                GameManager.Instance.ChangeStateNetwork(GameManager.GameState.GameOver);
            }
        }

        // ===== 기존 적 인수 =====

        /// <summary>
        /// 호스트 전환 후 기존 적 처리.
        /// 이전 호스트가 관리하던 적들은 AI가 멈춘 상태.
        /// 비상 보스전 시작 시 일반 적은 필요 없으므로 전부 정리.
        /// </summary>
        private void AdoptOrphanedEnemies()
        {
            var enemies = GameObject.FindGameObjectsWithTag("Enemy");
            int count = 0;

            foreach (var enemyObj in enemies)
            {
                // Boss는 건드리지 않음
                if (enemyObj.GetComponent<Boss>() != null) continue;

                // ForceReturn은 이벤트 구독이 끊겨 동작 안 함
                // 직접 비활성화로 처리
                enemyObj.SetActive(false);
                count++;
            }

            Debug.Log($"[HostMigration] 기존 적 {count}마리 정리 완료");
        }

        // ===== 일반 플레이어 퇴장 처리 =====

        private void HandlePlayerDisconnect(Player leftPlayer)
        {
            // 해당 플레이어의 PhotonView 찾기 → 사망 처리
            var playerObjects = GameObject.FindGameObjectsWithTag("Player");
            foreach (var obj in playerObjects)
            {
                var pv = obj.GetComponent<PhotonView>();
                if (pv != null && pv.Owner != null &&
                    pv.Owner.ActorNumber == leftPlayer.ActorNumber)
                {
                    // 해당 플레이어 비활성화
                    obj.SetActive(false);

                    // RespawnManager에서 제거
                    if (RespawnManager.Instance != null)
                        RespawnManager.Instance.UnregisterPlayer(pv.ViewID);

                    Debug.Log($"[HostMigration] 퇴장 플레이어 비활성화: {leftPlayer.NickName}");
                    break;
                }
            }
        }
    }
}