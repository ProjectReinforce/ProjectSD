using Adapter.Manager;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

namespace Adapter.UI.Menu
{
    public class TitlePanelController : MonoBehaviour
    {
        [SerializeField] private MenuSceneManager menuSceneManager;
        [SerializeField] private Button soloPlayButton;
        [SerializeField] private Button multiPlayButton;
        [SerializeField] private Button settingsButton;

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
            // 콜백 타이밍 이슈가 있어도 이미 방에 들어간 상태라면 대기실로 강제 전환.
            if (PhotonNetwork.InRoom)
            {
                if (menuSceneManager == null)
                {
                    menuSceneManager = FindAnyObjectByType<MenuSceneManager>();
                }

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
            // 혼자하기 흐름: (필요 시) 접속 -> 비노출 솔로 방 생성 -> 대기실 이동.
            pendingSoloCreate = true;
            pendingGoRoomList = false;
            TryConnectOrRunPendingAction();
        }

        public void OnClickJoinMultiplayer()
        {
            // 같이하기 흐름: (필요 시) 접속 -> 공개 방 목록 화면 이동.
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

            // 접속이 끝나면 콜백에서 보류 중인 액션을 실행.
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
