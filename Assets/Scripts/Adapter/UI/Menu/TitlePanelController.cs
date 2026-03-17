using Adapter.Manager;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

namespace Adapter.UI.Menu
{
    /// <summary>
    /// 타이틀 패널.
    /// 혼자하기 / 같이하기 / 설정 / 종료 버튼 처리.
    ///
    /// Update()에서 InRoom 폴링:
    ///   NetworkManager가 DontDestroyOnLoad이므로 씬 전환 후
    ///   Photon 콜백(OnJoinedRoom)이 누락될 수 있음.
    ///   InRoom 상태를 매 프레임 체크해서 대기실 전환을 보장.
    /// </summary>
    public class TitlePanelController : MonoBehaviour
    {
        [SerializeField] private MenuSceneManager menuSceneManager;
        [Header("연결 대기")]
        [SerializeField] private Button soloPlayButton;
        [SerializeField] private Button multiPlayButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private GameObject connectingPanel;

        private bool pendingSoloCreate;
        private bool pendingGoRoomList;
        private bool lastInteractableState;

        private void OnEnable()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.ConnectionStateChanged += HandleConnectionStateChanged;
                NetworkManager.Instance.JoinedRoom += HandleJoinedRoom;
                NetworkManager.Instance.CreateRoomFailed += HandleCreateRoomFailed;
                NetworkManager.Instance.Connect();
            }

            RefreshMenuButtons();
        }

        private void OnDisable()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.ConnectionStateChanged -= HandleConnectionStateChanged;
                NetworkManager.Instance.JoinedRoom -= HandleJoinedRoom;
                NetworkManager.Instance.CreateRoomFailed -= HandleCreateRoomFailed;
            }
        }

        private void Update()
        {
            // 방 입장 완료 감지 (콜백 누락 대비 폴링).
            // CreateSoloRoom() 후 OnJoinedRoom 콜백이 오지 않는 경우를 커버.
            if (PhotonNetwork.InRoom)
            {
                menuSceneManager?.ShowWaitingRoom();
                return;
            }

            // 이벤트 누락/순서 이슈가 있어도 로비 준비 상태를 폴링해 버튼 상태를 복구한다.
            var current = IsMenuActionReady();
            if (current != lastInteractableState)
            {
                RefreshMenuButtons();
            }
        }

        public void OnClickSoloPlay()
        {
            pendingSoloCreate = true;
            pendingGoRoomList = false;
            TryConnectOrRunPendingAction();
        }

        public void OnClickJoinMultiplayer()
        {
            pendingSoloCreate = false;
            pendingGoRoomList = true;
            TryConnectOrRunPendingAction();
        }

        public void OnClickSettings()
        {
            Debug.Log("Open settings popup (TODO)");
        }

        public void OnClickQuit()
        {
            Application.Quit();
        }

        private void TryConnectOrRunPendingAction()
        {
            if (NetworkManager.Instance == null)
            {
                return;
            }

            if (NetworkManager.Instance.IsConnected)
            {
                ExecutePendingAction();
                return;
            }

            NetworkManager.Instance.Connect();
        }

        private void HandleConnectionStateChanged(bool connected)
        {
            RefreshMenuButtons();

            if (!IsMenuActionReady())
            {
                return;
            }

            ExecutePendingAction();
        }

        private void ExecutePendingAction()
        {
            if (NetworkManager.Instance == null)
            {
                return;
            }

            if (!IsMenuActionReady())
            {
                return;
            }

            if (pendingSoloCreate)
            {
                pendingSoloCreate = false;
                NetworkManager.Instance.CreateSoloRoom();
                return;
            }

            if (pendingGoRoomList)
            {
                pendingGoRoomList = false;
                menuSceneManager?.ShowRoomList();
            }
        }

        private void HandleJoinedRoom()
        {
            menuSceneManager?.ShowWaitingRoom();
        }

        private void HandleCreateRoomFailed(short returnCode, string message)
        {
            pendingSoloCreate = false;
            Debug.LogWarning($"Solo room create failed ({returnCode}): {message}");
        }

        private bool IsMenuActionReady()
        {
            return NetworkManager.Instance != null && NetworkManager.Instance.IsMatchmakingReady;
        }

        private void RefreshMenuButtons()
        {
            var interactable = IsMenuActionReady();
            lastInteractableState = interactable;

            // 연결 완료 전까지 차단 패널 표시
            if (connectingPanel != null)
                connectingPanel.SetActive(!interactable);

            if (soloPlayButton != null)
            {
                soloPlayButton.interactable = interactable;
            }

            if (multiPlayButton != null)
            {
                multiPlayButton.interactable = interactable;
            }

            if (settingsButton != null)
            {
                settingsButton.interactable = interactable;
            }
        }
    }
}
