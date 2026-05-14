using Photon.Pun;
using SwDreams.Features.Map.Adapter.Data;
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
        [Tooltip("MapDatabase 미연결 또는 방 props 의 mapId 매칭 실패 시 사용할 폴백 씬 이름.")]
        [SerializeField] private string gameSceneName = "GameScene";

        [Tooltip("등록된 맵 목록. 방 생성 시 호스트가 선택한 mapId 로 조회하여 해당 맵의 sceneName 로드.")]
        [SerializeField] private MapDatabase mapDatabase;

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
                NetworkManager.Instance?.RequestLoadGameScene(ResolveGameSceneName());
            }
        }

        /// <summary>
        /// 방 props 의 mapId → MapDatabase 조회 → 해당 맵의 sceneName 반환.
        /// 매칭 실패 시 인스펙터 폴백(gameSceneName) 으로 폴백.
        /// </summary>
        private string ResolveGameSceneName()
        {
            if (mapDatabase == null) return gameSceneName;

            var mapId = NetworkManager.GetRoomMapId(PhotonNetwork.CurrentRoom);
            var map = mapDatabase.GetById(mapId) ?? mapDatabase.DefaultMap;
            if (map == null || string.IsNullOrEmpty(map.GameSceneName)) return gameSceneName;
            return map.GameSceneName;
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
