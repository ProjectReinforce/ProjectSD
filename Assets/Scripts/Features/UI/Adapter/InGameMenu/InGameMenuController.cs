using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Photon.Pun;
using SwDreams.Shared.Managers;
using SwDreams.Features.UI.Adapter.Menu;
using SwDreams.Features.UI.Presentation;

namespace SwDreams.Features.UI.Adapter.InGameMenu
{
    /// <summary>
    /// 인게임 ESC 메뉴 컨트롤러. spec: [docs/systems/in-game-menu.md].
    ///
    /// 정책:
    ///   - 솔로 (PhotonNetwork.CurrentRoom.PlayerCount == 1) + 현재 Playing/BossFight → GameState.Paused 로 진정 정지
    ///   - 멀티 (2명 이상) → GameState 안 건드림. 로컬 UI 토글만.
    ///   - Paused (레벨업 중) 에서 ESC → GameState 안 건드림. UI 만 띄움 (LevelUpManager 가 Paused 권한자).
    ///   - Loading/GameClear/GameOver 면 ESC 무시.
    ///
    /// 호스트가 종료해도 정지 동기화는 하지 않는다 (멀티 로컬 토글 정책).
    ///
    /// 셋업: InGameMenuCanvas (sortOrder=100) 의 루트에 본 컴포넌트 부착.
    ///       4 버튼 + SettingsPanelUI 인스턴스를 인스펙터 연결.
    ///       ConfirmDialog 는 시스템 오브젝트(DontDestroyOnLoad) 자식의 글로벌 싱글톤을 정적 호출로 사용.
    /// </summary>
    public class InGameMenuController : MonoBehaviour
    {
        [Header("UI 루트")]
        [Tooltip("메뉴 카드 (Dim + 4 버튼). SetActive 토글 대상.")]
        [SerializeField] private GameObject menuRoot;

        [Header("버튼")]
        [SerializeField] private Button btnResume;
        [SerializeField] private Button btnSettings;
        [SerializeField] private Button btnLeaveRoom;
        [SerializeField] private Button btnQuitGame;

        [Header("하위 패널")]
        [Tooltip("같은 캔버스 하위에 배치된 SettingsPanelUI 인스턴스. (사용자가 prefab 인스턴스화 후 연결)")]
        [SerializeField] private SettingsPanelUI settingsPanel;

        // ===== 상태 =====
        private bool isOpen;

        // 솔로 진정 정지 시 직전 상태 캐싱 (Playing↔BossFight 정확 복원).
        private GameManager.GameState cachedPrevState;
        private bool didPauseGame;

        // 솔로 보조 정지 (LevelUp 타이머 등 외부 정지원 가드용).
        // GameState 캐싱과 별개 — 레벨업 중 ESC (cachedPrevState=Paused) 에서도 set 하기 위해.
        private bool didMenuPause;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            if (menuRoot != null) menuRoot.SetActive(false);

            if (btnResume != null) btnResume.onClick.AddListener(Close);
            if (btnSettings != null) btnSettings.onClick.AddListener(OnClickSettings);
            if (btnLeaveRoom != null) btnLeaveRoom.onClick.AddListener(OnClickLeaveRoom);
            if (btnQuitGame != null) btnQuitGame.onClick.AddListener(OnClickQuitGame);
        }

        private void OnDestroy()
        {
            if (btnResume != null) btnResume.onClick.RemoveListener(Close);
            if (btnSettings != null) btnSettings.onClick.RemoveListener(OnClickSettings);
            if (btnLeaveRoom != null) btnLeaveRoom.onClick.RemoveListener(OnClickLeaveRoom);
            if (btnQuitGame != null) btnQuitGame.onClick.RemoveListener(OnClickQuitGame);

            // LeaveRoom 콜백 도착 전에 컨트롤러가 파괴되는 케이스 (씬 전환·HostMigration) 보호.
            if (NetworkManager.Instance != null)
                NetworkManager.Instance.LeftRoom -= OnLeftRoomForExit;

            // 메뉴가 열린 채 컨트롤러가 파괴되면 정지 플래그가 영구화되는 것 방지.
            if (didMenuPause)
                GameManager.Instance?.SetMenuPaused(false);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (!kb.escapeKey.wasPressedThisFrame) return;

            // 확인 다이얼로그가 열려있으면 ESC 는 다이얼로그 취소로만 사용.
            var dialog = ConfirmDialog.Instance;
            if (dialog != null && dialog.IsOpen)
            {
                dialog.Cancel();
                return;
            }

            // 설정 패널이 열려있으면 ESC 는 설정 닫기로만 사용 (메뉴 자체는 유지).
            if (settingsPanel != null && settingsPanel.IsShown)
            {
                settingsPanel.Hide();
                return;
            }

            // 이미 열린 메뉴는 GameState 에 무관하게 닫기 허용.
            // (메뉴 떠있는 채로 GameOver/GameClear 진입 시에도 ESC 로 닫을 수 있어야 결과창에 접근 가능.)
            if (isOpen)
            {
                Close();
                return;
            }

            // 새로 여는 건 호출 가능 상태에서만.
            if (!CanOpenNow()) return;

            Open();
        }

        // ===== 호출 가능 상태 (열기 한정) =====

        private static bool CanOpenNow()
        {
            var gm = GameManager.Instance;
            if (gm == null) return false;

            switch (gm.CurrentState)
            {
                case GameManager.GameState.Playing:
                case GameManager.GameState.BossFight:
                case GameManager.GameState.Paused:
                    return true;
                default:
                    return false;
            }
        }

        // ===== Toggle =====

        public void Toggle()
        {
            if (isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (isOpen) return;
            isOpen = true;

            var gm = GameManager.Instance;
            if (gm != null)
            {
                cachedPrevState = gm.CurrentState;

                bool soloRoom = !PhotonNetwork.InRoom
                    || PhotonNetwork.CurrentRoom == null
                    || PhotonNetwork.CurrentRoom.PlayerCount <= 1;

                // 솔로 + Playing/BossFight 일 때만 진정 정지. Paused (레벨업) 면 건드리지 않음.
                bool canPause = soloRoom &&
                    (cachedPrevState == GameManager.GameState.Playing ||
                     cachedPrevState == GameManager.GameState.BossFight);

                if (canPause)
                {
                    gm.ChangeState(GameManager.GameState.Paused);
                    didPauseGame = true;
                }

                // 솔로면 LevelUp 타이머 등 외부 정지원도 차단 (레벨업 중 ESC 포함).
                // 멀티는 set 안 함 — 게임 흐름 유지 정책.
                if (soloRoom)
                {
                    gm.SetMenuPaused(true);
                    didMenuPause = true;
                }
            }

            if (menuRoot != null) menuRoot.SetActive(true);
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;

            // 설정 패널이 떠있는 채로 메뉴를 닫는 케이스 보호.
            if (settingsPanel != null && settingsPanel.IsShown)
                settingsPanel.Hide();

            if (menuRoot != null) menuRoot.SetActive(false);

            if (didPauseGame)
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.CurrentState == GameManager.GameState.Paused)
                    gm.ChangeState(cachedPrevState);
                didPauseGame = false;
            }

            if (didMenuPause)
            {
                GameManager.Instance?.SetMenuPaused(false);
                didMenuPause = false;
            }
        }

        // ===== 메뉴 항목 =====

        private void OnClickSettings()
        {
            if (settingsPanel == null)
            {
                Debug.LogWarning("[InGameMenuController] settingsPanel 미연결. 인스펙터에서 SettingsPanel 인스턴스를 연결하세요.");
                return;
            }
            settingsPanel.Show();
        }

        private void OnClickLeaveRoom()
        {
            // 글로벌 싱글톤 정적 호출 — 인스턴스 없으면 안전 fallback 으로 즉시 실행.
            ConfirmDialog.Show(
                title: "룸 나가기",
                message: "현재 게임을 떠나 룸 리스트로 돌아갑니다. 진행 상황은 저장되지 않습니다.",
                onConfirm: LeaveRoomImmediate);
        }

        private void OnClickQuitGame()
        {
            ConfirmDialog.Show(
                title: "게임 종료",
                message: "게임을 완전히 종료합니다.",
                onConfirm: QuitGameImmediate);
        }

        // ===== 실제 동작 =====

        private void LeaveRoomImmediate()
        {
            // ResultManager.OnExit 와 동일 패턴: 룸 리스트 진입 플래그 + LeaveRoom + 씬 전환.
            PhotonNetwork.AutomaticallySyncScene = false;
            MenuSceneManager.ReturnToRoomList = true;

            // 정지 상태였다면 풀어줘야 새 씬에서 잔재 없음.
            if (didPauseGame)
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.CurrentState == GameManager.GameState.Paused)
                    gm.ChangeState(cachedPrevState);
                didPauseGame = false;
            }

            if (didMenuPause)
            {
                GameManager.Instance?.SetMenuPaused(false);
                didMenuPause = false;
            }

            // 메뉴 UI 즉시 닫기 (씬 전환 1프레임 사이 잔재 방지).
            if (menuRoot != null) menuRoot.SetActive(false);
            isOpen = false;

            if (NetworkManager.Instance != null && PhotonNetwork.InRoom)
            {
                NetworkManager.Instance.LeftRoom += OnLeftRoomForExit;
                NetworkManager.Instance.LeaveRoom();
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MenuScene");
            }
            Debug.Log("[InGameMenuController] 룸 나가기 → 룸 리스트");
        }

        private void OnLeftRoomForExit()
        {
            if (NetworkManager.Instance != null)
                NetworkManager.Instance.LeftRoom -= OnLeftRoomForExit;
            UnityEngine.SceneManagement.SceneManager.LoadScene("MenuScene");
        }

        private static void QuitGameImmediate()
        {
            Debug.Log("[InGameMenuController] 게임 종료");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
