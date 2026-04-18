using System.Collections;
using SwDreams.Features.UI.Presentation;
using SwDreams.Features.Progression.Adapter;
using SwDreams.Features.Boss.Adapter;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using SwDreams.Shared.Data;
using SwDreams.Shared.Domain.Interfaces;

namespace SwDreams.Shared.Managers
{
    /// <summary>
    /// 호스트 이탈 감지 + 재연결 대기 + GameTime 기준 게임 재개.
    ///
    /// Photon PUN2의 자동 MasterClient 전환 활용:
    /// - OnMasterClientSwitched: 호스트 변경 감지
    /// - OnPlayerLeftRoom: 플레이어 퇴장 감지
    ///
    /// 플로우:
    /// 1. 호스트 퇴장 감지
    /// 2. 게임 일시정지 + ReconnectUI 표시
    /// 3. reconnectWaitTime 초 대기
    /// 4. 모든 적/보스 정리 → Playing 상태로 전환
    /// 5. SpawnManager/BossSpawner가 GameTime 기준으로 자연 재개
    ///    (새 인원수 기준 스케일링 자동 적용)
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
        /// 레벨업 종료 후 게임을 재개해야 하는지 플래그.
        /// LevelUpManager.EndLevelUpSequence()에서 확인.
        /// </summary>
        public bool PendingGameResume { get; private set; } = false;

        private IEnumerator HandleHostMigration()
        {
            if (isMigrating) yield break;
            isMigrating = true;

            OnMigrationStarted?.Invoke();

            bool wasInLevelUp = GameManager.Instance != null &&
                                GameManager.Instance.CurrentState == GameManager.GameState.Paused;

            if (wasInLevelUp)
            {
                // 레벨업 중: 대기 없이 즉시 세션 인수
                // 레벨업 완료 후 ResumeGameFromCurrentTime 호출
                Debug.Log("[HostMigration] 레벨업 중 호스트 이탈 — 세션 인수");

                if (PhotonNetwork.IsMasterClient)
                {
                    if (LevelUpManager.Instance != null)
                        LevelUpManager.Instance.AdoptLevelUpSession();

                    // 레벨업 완료 시 ResumeGameFromCurrentTime 호출 예약
                    PendingGameResume = true;
                }

                isMigrating = false;
                OnMigrationCompleted?.Invoke();
            }
            else
            {
                // Playing/BossFight: 잠시 대기 후 GameTime 기준으로 게임 재개
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
                    ResumeGameFromCurrentTime();
            }
        }

        /// <summary>
        /// LevelUpManager.EndLevelUpSequence()에서 호출.
        /// 레벨업 완료 후 대기 중이던 게임 재개 실행.
        /// </summary>
        public void OnLevelUpCompleted()
        {
            if (!PendingGameResume) return;
            PendingGameResume = false;

            Debug.Log("[HostMigration] 레벨업 완료 → GameTime 기준 게임 재개");
            ResumeGameFromCurrentTime();
        }

        // ===== 게임 재개 (새 전략) =====

        /// <summary>
        /// 모든 적/보스를 정리하고 GameTime 기준으로 게임을 재개.
        /// - GameTime >= bossSpawnTime → BossSpawner.Update()가 보스 재스폰
        /// - GameTime < bossSpawnTime → SpawnManager가 해당 웨이브부터 재개
        /// 인원수 변동이 자연스럽게 반영됨.
        /// </summary>
        private void ResumeGameFromCurrentTime()
        {
            float gameTime = GameManager.Instance?.GameTime ?? 0f;
            float bossTime = GameManager.Instance?.Config?.bossSpawnTime ?? 600f;
            Debug.Log($"[HostMigration] 게임 재개 — GameTime:{gameTime:F1}s, BossTime:{bossTime:F1}s");

            // 1) BossPhaseManager 정리 (보스전 중이었으면)
            if (BossPhaseManager.Instance != null)
                BossPhaseManager.Instance.EndBossFight();

            // 2) 모든 적 + 보스 정리 → 스폰 상태 리셋
            if (SpawnManager.Instance != null)
                SpawnManager.Instance.ResetForMigration();

            if (BossSpawner.Instance != null)
                BossSpawner.Instance.ResetForMigration();

            // 3) 남은 플레이어 전원 사망 체크
            //    2인 플레이 중 살아있던 호스트가 나가고, 죽어있는 클라이언트만 남으면 GameOver
            if (AreAllRemainingPlayersDead())
            {
                Debug.Log("[HostMigration] 남은 플레이어 전원 사망 → GameOver");
                GameManager.Instance?.ChangeStateNetwork(GameManager.GameState.GameOver);
                return;
            }

            // 4) Playing 상태로 전환
            //    → SpawnManager.Update()가 GameTime 기반으로 적 스폰 재개
            //    → BossSpawner.Update()가 GameTime >= bossTime이면 보스 경고 → 스폰
            GameManager.Instance?.ChangeStateNetwork(GameManager.GameState.Playing);

            Debug.Log("[HostMigration] 게임 재개 완료 — 새 인원수 기준으로 리스폰");
        }

        /// <summary>
        /// 남은 플레이어가 전원 사망 상태인지 확인.
        /// IDamageable.IsAlive로 직접 체크 (RespawnManager 상태에 의존하지 않음).
        /// </summary>
        private bool AreAllRemainingPlayersDead()
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            if (players.Length == 0) return true;

            foreach (var p in players)
            {
                if (!p.activeInHierarchy) continue;
                var damageable = p.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                    return false; // 한 명이라도 살아있으면 계속
            }
            return true;
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
                    if (SwDreams.Features.Character.Adapter.RespawnManager.Instance != null)
                        SwDreams.Features.Character.Adapter.RespawnManager.Instance.UnregisterPlayer(pv.ViewID);

                    Debug.Log($"[HostMigration] 퇴장 플레이어 비활성화: {leftPlayer.NickName}");
                    break;
                }
            }
        }
    }
}