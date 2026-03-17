using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Adapter.Manager
{
    public enum GameState
    {
        None,
        Menu,
        WaitingRoom,
        InGame,
        Result,
        Paused
    }

    /// <summary>
    /// 씬 전환 + 게임 상태 관리.
    ///
    /// 씬 전환 정책:
    ///   - 게임 시작: 호스트가 RPC로 전체 클라이언트에 씬 전환 지시.
    ///     AutomaticallySyncScene에 의존하지 않음.
    ///   - 다시 하기 / 나가기: 각 클라이언트가 독립적으로 SceneManager.LoadScene.
    ///
    /// AutomaticallySyncScene = false 상태를 유지.
    /// PhotonNetwork.LoadLevel() 대신 RPC + SceneManager.LoadScene 사용.
    ///
    /// 셋업: DontDestroyOnLoad 오브젝트에 TestManager + PhotonView 부착.
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class TestManager : MonoBehaviourPun
    {
        public static TestManager Instance { get; private set; }

        [SerializeField] private string menuSceneName = "MenuScene";
        [SerializeField] private string gameSceneName = "GameScene";

        public GameState CurrentState { get; private set; } = GameState.None;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // AutomaticallySyncScene은 사용하지 않음.
            // 게임 시작 시 RPC로 직접 씬 전환을 지시.
            PhotonNetwork.AutomaticallySyncScene = false;
        }

        /// <summary>
        /// 호스트가 호출. RPC로 모든 클라이언트에 게임씬 전환 지시.
        /// </summary>
        public void EnterGameSceneByMaster()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            photonView.RPC(nameof(RPC_LoadGameScene), RpcTarget.All);
        }

        [PunRPC]
        private void RPC_LoadGameScene()
        {
            SetState(GameState.InGame);
            SceneManager.LoadScene(gameSceneName);
            Debug.Log($"[TestManager] 게임씬 전환: {gameSceneName}");
        }

        public void ReturnToMenu()
        {
            SetState(GameState.Menu);
            SceneManager.LoadScene(menuSceneName);
        }

        public void ReturnToWaitingRoom()
        {
            SetState(GameState.WaitingRoom);
            SceneManager.LoadScene(menuSceneName);
        }

        private void SetState(GameState next)
        {
            if (CurrentState == next)
            {
                return;
            }

            CurrentState = next;
        }
    }
}
