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

    public class TestManager : MonoBehaviour
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
        }

        public void EnterGameSceneByMaster()
        {
            SetState(GameState.InGame);
            if (PhotonNetwork.IsMasterClient)
            {
                PhotonNetwork.LoadLevel(gameSceneName);
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
