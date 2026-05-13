using SwDreams.Shared.Managers;
using SwDreams.Features.UI.Adapter.Menu;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SwDreams.Features.UI.Adapter.Menu
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
        [SerializeField] private Button quitButton;
        [SerializeField] private GameObject connectingPanel;

        [Header("연결 상태 표시 (connectingPanel 자식)")]
        [Tooltip("connectingPanel 안의 안내 TMP_Text. 상태별 문구로 갱신.")]
        [SerializeField] private TMP_Text connectingStatusText;
        [Tooltip("Failed 상태에서만 활성. 누르면 NetworkManager.RetryConnect() 호출.")]
        [SerializeField] private Button retryButton;
        [Tooltip("재시도 버튼 누른 직후 다시 누르기까지 쿨다운(초).")]
        [SerializeField] private float retryButtonCooldown = 3f;

        // R12 설정 패널: 코드 의존 제거 (Adapter→Presentation 역방향 의존 방지).
        // settingsButton.onClick 인스펙터에서 SettingsPanelUI.Show 직접 연결 — Unity-native first 패턴.

        private bool pendingSoloCreate;
        private bool pendingGoRoomList;
        private bool lastInteractableState;
        private float retryCooldownRemaining;

        private void OnEnable()
        {
            // 다시하기로 MenuScene 재진입 시 TitlePanel 이 인스펙터 default active 로 잠깐 켜졌다가
            // MenuSceneManager.Start 의 ShowWaitingRoom 으로 꺼지는 race 가 있다.
            // 이 짧은 enable 사이에 Connect() 가 호출되면 InRoom 상태에서 새 접속 사이클로 진입하며
            // 워치독 timeout 으로 룸에서 튕긴다. NetworkManager.Connect 내부에도 동일 가드가 있지만,
            // 이벤트 구독까지 한 사이클 끼는 게 의미 없으므로 여기서 조기 return.
            if (PhotonNetwork.InRoom)
            {
                return;
            }

            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.ConnectionStateChanged += HandleConnectionStateChanged;
                NetworkManager.Instance.StateChanged += HandleStateChanged;
                NetworkManager.Instance.JoinedRoom += HandleJoinedRoom;
                NetworkManager.Instance.CreateRoomFailed += HandleCreateRoomFailed;
                NetworkManager.Instance.Connect();
            }

            if (retryButton != null)
            {
                retryButton.onClick.AddListener(OnClickRetry);
            }

            RefreshMenuButtons();
            RefreshConnectingPanel();
        }

        private void OnDisable()
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.ConnectionStateChanged -= HandleConnectionStateChanged;
                NetworkManager.Instance.StateChanged -= HandleStateChanged;
                NetworkManager.Instance.JoinedRoom -= HandleJoinedRoom;
                NetworkManager.Instance.CreateRoomFailed -= HandleCreateRoomFailed;
            }

            if (retryButton != null)
            {
                retryButton.onClick.RemoveListener(OnClickRetry);
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

            // 재시도 버튼 쿨다운.
            if (retryCooldownRemaining > 0f)
            {
                retryCooldownRemaining -= Time.unscaledDeltaTime;
                if (retryCooldownRemaining < 0f) retryCooldownRemaining = 0f;
            }

            // Retrying 카운트다운/쿨다운 등 매 프레임 변하는 부분이 있어 폴링 갱신.
            // 패널이 켜져 있을 때만 비용 발생.
            if (connectingPanel != null && connectingPanel.activeSelf)
            {
                RefreshConnectingPanel();
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

        // OnClickSettings 제거 — settingsButton.onClick 에서 SettingsPanelUI.Show 직접 연결.
        // (Adapter→Presentation 역방향 의존 회피)

        public void OnClickQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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

        private void HandleStateChanged(ConnectionState state)
        {
            RefreshConnectingPanel();
        }

        private void OnClickRetry()
        {
            if (retryCooldownRemaining > 0f) return;
            if (NetworkManager.Instance == null) return;

            retryCooldownRemaining = retryButtonCooldown;
            NetworkManager.Instance.RetryConnect();
            // 즉시 시각 피드백(상태가 Connecting/Retrying 으로 곧 바뀌지만 한 프레임 늦을 수 있음).
            RefreshConnectingPanel();
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

            // 종료 버튼은 연결 상태와 무관하게 항상 누를 수 있어야 한다.
            // 연결 실패 상태에서 사용자가 게임을 빠져나갈 유일한 길.
            if (quitButton != null)
            {
                quitButton.interactable = true;
            }
        }

        /// <summary>
        /// connectingPanel 표시 + 안내 문구 + 재시도 버튼을 NetworkManager.State 기반으로 갱신.
        /// 패널 자체의 SetActive 도 여기서 책임지므로 RefreshMenuButtons 와 분리.
        /// </summary>
        private void RefreshConnectingPanel()
        {
            var nm = NetworkManager.Instance;
            // NetworkManager 가 없으면 패널은 끔(타이틀씬에 진입은 했지만 매니저 미준비 케이스).
            var state = nm != null ? nm.State : ConnectionState.Idle;
            var showPanel = state != ConnectionState.Connected;

            if (connectingPanel != null)
            {
                connectingPanel.SetActive(showPanel);
            }

            if (!showPanel)
            {
                return;
            }

            // 문구 갱신.
            if (connectingStatusText != null && nm != null)
            {
                connectingStatusText.text = BuildStatusMessage(nm);
            }

            // 재시도 버튼은 Failed 일 때만 활성. 쿨다운 중이면 비활성.
            if (retryButton != null)
            {
                var failed = state == ConnectionState.Failed;
                retryButton.gameObject.SetActive(failed);
                retryButton.interactable = failed && retryCooldownRemaining <= 0f;
            }
        }

        private string BuildStatusMessage(NetworkManager nm)
        {
            switch (nm.State)
            {
                case ConnectionState.Connecting:
                    return "서버 연결 중...";
                case ConnectionState.Retrying:
                    var seconds = Mathf.CeilToInt(nm.RetryCountdownSeconds);
                    return $"연결 실패 — 재시도 중 ({nm.CurrentRetryAttempt}/{nm.MaxRetryAttempts})\n{seconds}초 후 다시 시도";
                case ConnectionState.Failed:
                    var causeText = nm.LastFailureCause.HasValue ? nm.LastFailureCause.Value.ToString() : "Unknown";
                    if (retryCooldownRemaining > 0f)
                    {
                        return $"서버 연결 실패\n원인: {causeText}\n재시도 가능까지 {Mathf.CeilToInt(retryCooldownRemaining)}초";
                    }
                    return $"서버 연결 실패\n원인: {causeText}";
                case ConnectionState.Idle:
                    return "연결 대기 중...";
                case ConnectionState.Connected:
                default:
                    return string.Empty;
            }
        }
    }
}
