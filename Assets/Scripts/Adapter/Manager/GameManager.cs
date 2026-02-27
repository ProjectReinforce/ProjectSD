using System;
using UnityEngine;
using Photon.Pun;
using SwDreams.Application;

namespace SwDreams.Adapter.Manager
{
    /// <summary>
    /// 게임 상태 + 경험치/레벨업 관리.
    /// 호스트가 경험치 계산 후 RPC로 전체 클라이언트에 동기화.
    /// 
    /// GameScene에 빈 GameObject → GameManager + PhotonView 부착.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class GameManager : MonoBehaviourPun
    {
        public static GameManager Instance { get; private set; }

        public enum GameState
        {
            Loading,
            Playing,
            Paused,
            BossFight,
            GameClear,
            GameOver
        }

        // Application 서비스
        private ExperienceService expService = new ExperienceService();

        // 상태
        public GameState CurrentState { get; private set; } = GameState.Loading;
        public int TeamLevel => expService.CurrentLevel;
        public int TeamExp => expService.CurrentExp;
        public float GameTime { get; private set; }

        // 이벤트
        public event Action<int, int> OnExpChanged;     // current, required
        public event Action<int> OnLevelUp;             // newLevel
        public event Action<GameState> OnStateChanged;

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
            if (CurrentState == GameState.Playing)
                GameTime += Time.deltaTime;
        }

        /// <summary>
        /// 경험치 추가. 호스트에서만 호출.
        /// 계산 후 RPC로 전체 클라이언트에 동기화.
        /// </summary>
        public void AddExp(int amount)
        {
            if (!PhotonNetwork.IsMasterClient) return;

            int leveledUp = expService.AddExp(amount);

            photonView.RPC(nameof(RPC_SyncExp), RpcTarget.All,
                expService.CurrentExp, expService.GetRequiredExp(), expService.CurrentLevel, leveledUp);
        }

        [PunRPC]
        private void RPC_SyncExp(int currentExp, int requiredExp, int level, int levelUpCount)
        {
            OnExpChanged?.Invoke(currentExp, requiredExp);

            // 레벨업 횟수만큼 이벤트 발행 → LevelUpManager가 큐에 쌓음
            for (int i = 0; i < levelUpCount; i++)
            {
                int lvl = level - levelUpCount + i + 1; // 중간 레벨도 정확히 전달
                Debug.Log($"[GameManager] 레벨업! Lv.{lvl}");
                OnLevelUp?.Invoke(lvl);
            }
        }

        public void ChangeState(GameState newState)
        {
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
        }

        /// <summary>
        /// 네트워크 상태 전환. 호스트에서만 호출.
        /// 모든 클라이언트에서 동시에 상태 변경.
        /// </summary>
        public void ChangeStateNetwork(GameState newState)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            photonView.RPC(nameof(RPC_ChangeState), RpcTarget.All, (int)newState);
        }

        [PunRPC]
        private void RPC_ChangeState(int stateInt)
        {
            GameState newState = (GameState)stateInt;
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
            Debug.Log($"[GameManager] 상태 전환(Network): {newState}");
        }
    }
}
