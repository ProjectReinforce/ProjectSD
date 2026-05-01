using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SwDreams.Shared.Managers
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

    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

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
        }

        public void EnterGameSceneByMaster()
        {
            SetState(GameState.InGame);
            if (PhotonNetwork.IsMasterClient)
            {
                // [B8 A-1] PhotonNetwork.LoadLevel 대신 명시적 RPC 사용.
                // LoadLevel 은 autoSync=true 일 때만 클라 자동 따라감. autoSync=false 정책상 RPC 로 동기화.
                NetworkManager.Instance?.RequestLoadGameScene(gameSceneName);
            }
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
