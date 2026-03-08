using System;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using SwDreams.Data;
using SwDreams.Domain.Interfaces;

namespace SwDreams.Adapter.Manager
{
    /// <summary>
    /// 부활 타이머 관리 + 전원 사망 판정 (호스트 전용).
    ///
    /// 플로우:
    /// 1. PlayerStub 사망 → RequestRespawn(viewID) 호출 (호스트)
    /// 2. respawnDelay 초 대기 (GameplayConfig)
    /// 3. 안전 지점 계산 → 부활 RPC
    /// 4. 전원 사망 체크 → GameOver
    ///
    /// 셋업: GameScene에 빈 GameObject "RespawnManager"
    ///        → RespawnManager + PhotonView 부착
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class RespawnManager : MonoBehaviourPun
    {
        public static RespawnManager Instance { get; private set; }

        // 부활 대기 중인 플레이어 (viewID → 남은 시간)
        private Dictionary<int, float> respawnTimers = new Dictionary<int, float>();

        // 사망 상태 추적 (viewID → 사망 여부)
        private Dictionary<int, bool> deadPlayers = new Dictionary<int, bool>();

        // ===== 이벤트 =====

        /// <summary>로컬 플레이어 부활 카운트다운 갱신. UI용.</summary>
        public event Action<float, float> OnLocalRespawnTimer; // remaining, total

        /// <summary>전원 사망 시 발생.</summary>
        public event Action OnAllPlayersDead;

        // 설정 캐시
        private float RespawnDelay
        {
            get
            {
                var cfg = GameManager.Instance?.Config;
                return cfg != null ? cfg.respawnDelay : 10f;
            }
        }

        private float RespawnHPRatio
        {
            get
            {
                var cfg = GameManager.Instance?.Config;
                return cfg != null ? cfg.respawnHPRatio : 0.5f;
            }
        }

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

        private void Update()
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (GameManager.Instance == null) return;

            var state = GameManager.Instance.CurrentState;
            if (state != GameManager.GameState.Playing &&
                state != GameManager.GameState.BossFight)
                return;

            UpdateRespawnTimers();
        }

        // ===== 외부 호출 =====

        /// <summary>
        /// 플레이어 사망 시 호출 (호스트만).
        /// 부활 타이머 시작 + 전원 사망 체크.
        /// </summary>
        public void RequestRespawn(int photonViewID)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            deadPlayers[photonViewID] = true;
            respawnTimers[photonViewID] = RespawnDelay;

            Debug.Log($"[RespawnManager] 부활 요청: ViewID {photonViewID}, {RespawnDelay}초 후 부활");

            // 전원 사망 체크
            if (AreAllPlayersDead())
            {
                Debug.Log("[RespawnManager] 전원 사망 → GameOver");
                respawnTimers.Clear(); // 부활 취소
                OnAllPlayersDead?.Invoke();
                GameManager.Instance.ChangeStateNetwork(GameManager.GameState.GameOver);
                return;
            }

            // 클라이언트에 카운트다운 시작 알림
            photonView.RPC(nameof(RPC_StartRespawnCountdown), RpcTarget.All,
                photonViewID, RespawnDelay);
        }

        /// <summary>
        /// 플레이어 등록 (사망 추적용). 스폰 시 호출.
        /// </summary>
        public void RegisterPlayer(int photonViewID)
        {
            deadPlayers[photonViewID] = false;
        }

        /// <summary>
        /// 플레이어 제거 (퇴장 시). 호스트에서 호출.
        /// </summary>
        public void UnregisterPlayer(int photonViewID)
        {
            deadPlayers.Remove(photonViewID);
            respawnTimers.Remove(photonViewID);
        }

        // ===== 타이머 업데이트 (호스트) =====

        private void UpdateRespawnTimers()
        {
            if (respawnTimers.Count == 0) return;

            // 복사본으로 순회 (Dictionary 수정 방지)
            var keys = new List<int>(respawnTimers.Keys);

            foreach (int viewID in keys)
            {
                respawnTimers[viewID] -= Time.deltaTime;

                if (respawnTimers[viewID] <= 0f)
                {
                    ExecuteRespawn(viewID);
                    respawnTimers.Remove(viewID);
                }
            }
        }

        private void ExecuteRespawn(int photonViewID)
        {
            PhotonView targetView = PhotonView.Find(photonViewID);
            if (targetView == null)
            {
                Debug.LogWarning($"[RespawnManager] ViewID {photonViewID} 찾을 수 없음");
                return;
            }

            // 부활 HP 계산
            var damageable = targetView.GetComponent<IDamageable>();
            int respawnHP = Mathf.Max(1, Mathf.RoundToInt(
                (damageable?.MaxHP ?? 100) * RespawnHPRatio));

            // 안전 지점 계산
            Vector2 safePos = CalculateSafePosition(targetView.transform.position);

            deadPlayers[photonViewID] = false;

            Debug.Log($"[RespawnManager] 부활 실행: ViewID {photonViewID}, HP={respawnHP}, 위치={safePos}");

            // 모든 클라이언트에 부활 알림
            photonView.RPC(nameof(RPC_ExecuteRespawn), RpcTarget.All,
                photonViewID, respawnHP, safePos.x, safePos.y);
        }

        /// <summary>
        /// 적이 가장 적은 방향으로 약간 이동한 위치 반환.
        /// 간단 구현: 현재 위치에서 랜덤 방향으로 3~5m 이동.
        /// TODO: 적 밀집도 기반 안전 지점 계산으로 고도화.
        /// </summary>
        private Vector2 CalculateSafePosition(Vector3 deathPosition)
        {
            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float dist = UnityEngine.Random.Range(3f, 5f);
            return new Vector2(
                deathPosition.x + Mathf.Cos(angle) * dist,
                deathPosition.y + Mathf.Sin(angle) * dist
            );
        }

        private bool AreAllPlayersDead()
        {
            if (deadPlayers.Count == 0) return false;

            foreach (var kvp in deadPlayers)
            {
                if (!kvp.Value) return false; // 한 명이라도 살아있으면 false
            }
            return true;
        }

        // ===== RPC =====

        [PunRPC]
        private void RPC_StartRespawnCountdown(int photonViewID, float totalTime)
        {
            // 로컬 플레이어의 카운트다운이면 UI 시작
            PhotonView targetView = PhotonView.Find(photonViewID);
            if (targetView != null && targetView.IsMine)
            {
                StartCoroutine(LocalRespawnCountdown(totalTime));
            }
        }

        [PunRPC]
        private void RPC_ExecuteRespawn(int photonViewID, int respawnHP, float posX, float posY)
        {
            PhotonView targetView = PhotonView.Find(photonViewID);
            if (targetView == null) return;

            // 위치 이동
            targetView.transform.position = new Vector3(posX, posY, 0f);

            // PlayerStub.Respawn 호출 (로컬에서 직접 처리)
            var playerStub = targetView.GetComponent<SwDreams.Testing.PlayerStub>();
            if (playerStub != null)
            {
                playerStub.LocalRespawn(respawnHP);
            }
        }

        private System.Collections.IEnumerator LocalRespawnCountdown(float totalTime)
        {
            float remaining = totalTime;
            while (remaining > 0f)
            {
                OnLocalRespawnTimer?.Invoke(remaining, totalTime);
                yield return null;
                remaining -= Time.deltaTime;
            }
            OnLocalRespawnTimer?.Invoke(0f, totalTime);
        }
    }
}