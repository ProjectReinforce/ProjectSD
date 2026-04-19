using System.Collections.Generic;
using SwDreams.Features.UI.Adapter.Menu;
using SwDreams.Features.Character.Adapter.Data;
using SwDreams.Shared.Managers;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SwDreams.Features.UI.Adapter.Menu
{
    /// <summary>
    /// 대기실(Lobby) 패널 컨트롤러.
    ///
    /// 변경 이력:
    ///   [1] 뒤로가기 → 방 리스트 패널로 이동 (기존: 타이틀)
    ///   [2] 캐릭터 셀렉트 버튼 → CharacterSelectUI 팝업 열기 → 확인 시 적용
    ///   [3] 준비 상태에서 캐릭터 셀렉트 차단 (버튼 비활성 + 팝업 강제 닫기)
    ///
    /// 설계 원칙:
    ///   SRP — 대기실 UI 흐름(준비/시작/카운트다운)만 담당.
    ///         캐릭터 선택의 데이터 바인딩은 CharacterSelectUI에 위임.
    ///   DIP — ICharacterSelectCallback을 구현하여 CharacterSelectUI로부터
    ///         역방향 의존 없이 선택 결과를 받는다.
    ///   OCP — 새로운 캐릭터가 추가되어도 이 클래스는 변경되지 않는다.
    ///         CharacterDatabase SO와 CharacterSelectUI만 수정하면 된다.
    /// </summary>
    public class WaitingRoomPanelController : MonoBehaviourPunCallbacks, ICharacterSelectCallback
    {
        private const string CountdownActiveKey = "startCountdownActive";
        private const string CountdownEndTimeKey = "startCountdownEndTime";

        [SerializeField] private MenuSceneManager menuSceneManager;
        [SerializeField] private float startCountdownSeconds = 3f;

        [Header("UI")]
        [SerializeField] private TMP_Text roomInfoText;
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private Toggle readyToggle;
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text readyStartButtonText;

        [Header("플레이어 리스트 (신규)")]
        [Tooltip("LobbyPlayerEntry 프리팹이 쌓일 컨테이너 (VerticalLayoutGroup).")]
        [SerializeField] private Transform lobbyEntryContainer;
        [Tooltip("플레이어 리스트 1행 프리팹.")]
        [SerializeField] private LobbyPlayerEntry lobbyEntryPrefab;

        [Header("캐릭터 선택")]
        [Tooltip("캐릭터 셀렉트 팝업을 여는 버튼")]
        [SerializeField] private Button characterSelectButton;
        [Tooltip("CharacterSelectUI가 부착된 팝업 패널")]
        [SerializeField] private CharacterSelectUI characterSelectUI;

        [Header("대기실 월드")]
        [Tooltip("방 입장/퇴장에 맞춰 LobbyPlayer를 스폰/파괴.")]
        [SerializeField] private LobbyPlayerSpawner lobbyPlayerSpawner;

        private int displayedCountdown = -1;
        private bool isLoadingGameScene;
        private readonly List<LobbyPlayerEntry> entryPool = new List<LobbyPlayerEntry>();

        // ===================================================================
        // MonoBehaviour / PunCallbacks 라이프사이클
        // ===================================================================

        public override void OnEnable()
        {
            base.OnEnable();
            EnsureUiReferences();

            if (NetworkManager.Instance == null)
            {
                SetStateText("NetworkManager not found.");
                return;
            }

            // ready 상태 초기화 (이전 게임의 ready가 남아있을 수 있음)
            NetworkManager.Instance.SetLocalReady(false);
            isLoadingGameScene = false;

            // 디폴트 캐릭터(0) 보정: 방 최초 입장이면 characterId가 아직 없으므로 0으로 세팅.
            // 기존에 선택했던 값이 있으면 유지.
            if (PhotonNetwork.InRoom &&
                !NetworkManager.Instance.TryGetCharacterId(PhotonNetwork.LocalPlayer, out _))
            {
                NetworkManager.Instance.SetLocalCharacter(0);
            }

            // 이전 게임의 카운트다운 잔존 데이터 제거 (마스터만)
            if (PhotonNetwork.IsMasterClient)
            {
                CancelCountdown();

                // 게임씬에서 돌아왔을 때 로비 목록에 다시 표시
                if (PhotonNetwork.CurrentRoom != null)
                {
                    PhotonNetwork.CurrentRoom.IsVisible = true;
                    PhotonNetwork.CurrentRoom.IsOpen = true;
                }
            }

            NetworkManager.Instance.PlayersInRoomChanged += HandlePlayersChanged;
            NetworkManager.Instance.LeftRoom += HandleLeftRoom;

            // 캐릭터 선택 팝업 초기 상태: 닫힘
            if (characterSelectUI != null)
            {
                characterSelectUI.Close();
            }

            // 캐릭터 셀렉트 버튼 리스너 등록 (Remove→Add로 중복 누적 방지)
            if (characterSelectButton != null)
            {
                characterSelectButton.onClick.RemoveListener(OnClickCharacterSelect);
                characterSelectButton.onClick.AddListener(OnClickCharacterSelect);
            }

            // Start 버튼 리스너 등록 (호스트 수동 시작 트리거)
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnClickStartGame);
                startButton.onClick.AddListener(OnClickStartGame);
            }

            // 방에 입장된 상태에서만 LobbyPlayer 스폰.
            if (lobbyPlayerSpawner != null && PhotonNetwork.InRoom)
            {
                lobbyPlayerSpawner.Spawn();
            }

            RefreshRoomUi();
            RefreshRoleUi();
        }

        public override void OnDisable()
        {
            base.OnDisable();
            if (NetworkManager.Instance == null)
            {
                return;
            }

            NetworkManager.Instance.PlayersInRoomChanged -= HandlePlayersChanged;
            NetworkManager.Instance.LeftRoom -= HandleLeftRoom;

            // 캐릭터 셀렉트 버튼 리스너 해제
            if (characterSelectButton != null)
            {
                characterSelectButton.onClick.RemoveListener(OnClickCharacterSelect);
            }

            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnClickStartGame);
            }

            // 대기실을 떠나거나 게임씬으로 들어가기 전에 본인 LobbyPlayer 파괴.
            // isLoadingGameScene이면 씬 전환이 파괴를 처리하므로 생략 가능하지만,
            // 안전하게 Despawn 호출 (PhotonNetwork.InRoom 체크 내장).
            if (lobbyPlayerSpawner != null && !isLoadingGameScene)
            {
                lobbyPlayerSpawner.Despawn();
            }

            // 풀 엔트리는 패널(lobbyEntryContainer)과 함께 비활성되지만,
            // 다음 OnEnable에서 처음부터 재사용하기 위해 참조를 끊어둔다.
            // (파괴된 엔트리 참조가 리스트에 잔존해 NullRef를 유발하는 것을 방지)
            entryPool.RemoveAll(e => e == null);
        }

        private void Update()
        {
            UpdateCountdownUiAndStart();
        }

        // ===================================================================
        // PunCallbacks 오버라이드
        // ===================================================================

        public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            string playerName = otherPlayer.NickName;
            if (string.IsNullOrEmpty(playerName))
                playerName = $"Player {otherPlayer.ActorNumber}";

            SetStateText($"{playerName} 님이 퇴장했습니다.");
            Debug.Log($"[WaitingRoom] {playerName} 퇴장 (남은 인원: {PhotonNetwork.CurrentRoom.PlayerCount})");
        }

        /// <summary>
        /// 호스트가 방을 떠나 마스터가 이양되면 로컬의 Kick/Start 권한도 바뀐다.
        /// HandlePlayersChanged를 재호출해 엔트리/역할 UI 갱신 + 카운트다운 검증까지 한 번에 수행.
        /// </summary>
        public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
        {
            HandlePlayersChanged();
        }

        public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            if (propertiesThatChanged == null)
            {
                return;
            }

            if (!propertiesThatChanged.ContainsKey(CountdownActiveKey) &&
                !propertiesThatChanged.ContainsKey(CountdownEndTimeKey))
            {
                return;
            }

            displayedCountdown = -1;
            UpdateCountdownUiAndStart();

            // 카운트다운 시작/취소 즉시 버튼 interactable을 갱신해야
            // 호스트의 Start 버튼이 카운트다운 진입과 동시에 비활성화된다.
            RefreshRoleUi();
        }

        // ===================================================================
        // ICharacterSelectCallback 구현
        // ===================================================================

        /// <summary>
        /// CharacterSelectUI에서 확인 버튼을 눌렀을 때 콜백.
        /// 네트워크에 캐릭터 ID를 전파하고 UI를 갱신한다.
        /// </summary>
        public void OnCharacterConfirmed(int characterId)
        {
            NetworkManager.Instance?.SetLocalCharacter(characterId);
            SetStateText($"캐릭터 선택 완료: {characterId}");
            Debug.Log($"[WaitingRoom] Character confirmed: {characterId}");
            RefreshRoomUi();
        }

        // ===================================================================
        // 캐릭터 선택 — 요구사항 [2], [3]
        // ===================================================================

        /// <summary>
        /// 캐릭터 셀렉트 버튼 클릭 핸들러 (토글 동작).
        ///
        /// 동작:
        ///   - Ready 또는 카운트다운 활성 시: 어떤 입력도 무시.
        ///   - 팝업이 이미 열려 있으면 닫는다. (두 번째 클릭으로 취소 가능)
        ///   - 닫혀 있으면 연다.
        ///
        /// 왜 interactable 대신 코드 가드도 넣는가:
        ///   interactable = false 상태에서도 코드에서 onClick.Invoke()를 호출할 수 있다.
        ///   방어적 프로그래밍으로 양쪽 모두 막는 것이 안전하다.
        /// </summary>
        private void OnClickCharacterSelect()
        {
            if (IsLocalPlayerReady())
            {
                SetStateText("준비 상태에서는 캐릭터를 변경할 수 없습니다.");
                return;
            }

            if (IsCountdownActive())
            {
                SetStateText("카운트다운 중에는 캐릭터를 변경할 수 없습니다.");
                return;
            }

            if (characterSelectUI == null)
            {
                Debug.LogWarning("[WaitingRoom] CharacterSelectUI 참조가 없습니다.");
                return;
            }

            if (characterSelectUI.IsOpen)
            {
                characterSelectUI.Close();
                SetStateText(string.Empty);
                return;
            }

            characterSelectUI.Open(this); // this = ICharacterSelectCallback
            SetStateText("캐릭터를 선택하세요.");
        }

        // ===================================================================
        // 준비/시작 버튼
        // ===================================================================

        public void OnToggleReady(bool ready)
        {
            if (NetworkManager.Instance == null || !PhotonNetwork.InRoom)
            {
                return;
            }

            NetworkManager.Instance.SetLocalReady(ready);
            SetStateText(ready ? "준비 완료." : "준비 취소.");

            // 준비 상태 변경 시 캐릭터 선택 UI 잠금 재평가
            SyncCharacterSelectLockState();

            RefreshRoleUi();
        }

        public void OnClickReadyOrStart()
        {
            if (!PhotonNetwork.InRoom || NetworkManager.Instance == null)
            {
                return;
            }

            var currentReady = NetworkManager.Instance.IsPlayerReady(PhotonNetwork.LocalPlayer);
            OnToggleReady(!currentReady);
        }

        public void OnClickStartOrReady()
        {
            OnClickReadyOrStart();
        }

        /// <summary>
        /// Start/Ready 겸용 버튼 클릭 핸들러.
        ///
        /// 무엇: 호스트면 카운트다운 시작, 클라면 준비 상태 토글.
        /// 왜:   대기실 메인 버튼을 둘로 쪼개지 않고 하나로 겸용한다. 텍스트는 RefreshRoleUi가 전환.
        /// 어떻게: IsMasterClient 분기 → 호스트는 CanStart 충족 시 StartCountdown,
        ///        클라는 기존 OnClickReadyOrStart(Ready 토글)로 위임.
        /// </summary>
        public void OnClickStartGame()
        {
            if (!PhotonNetwork.InRoom || NetworkManager.Instance == null) return;

            if (!PhotonNetwork.IsMasterClient)
            {
                // 클라: Ready 토글
                OnClickReadyOrStart();
                return;
            }

            // 이하 호스트 분기
            // 카운트다운 중이면 같은 버튼이 "취소" 시맨틱 → 취소 경로.
            if (IsCountdownActive())
            {
                SetStateText("카운트다운을 취소했습니다.");
                CancelCountdown();
                return;
            }

            if (!NetworkManager.Instance.CanMasterStartGameInCurrentRoom())
            {
                SetStateText("모든 플레이어가 준비되어야 시작할 수 있습니다.");
                return;
            }

            SetStateText("게임을 시작합니다...");
            StartCountdown();
        }

        // ===================================================================
        // 뒤로가기 — 요구사항 [1]
        // ===================================================================

        /// <summary>
        /// 뒤로가기 버튼 클릭.
        ///
        /// 변경 전: HandleLeftRoom() → ShowTitle()
        /// 변경 후: HandleLeftRoom() → ShowRoomList()
        ///
        /// 왜 ShowRoomList()인가:
        ///   대기실은 "방 리스트에서 방을 선택해 진입한 곳"이므로,
        ///   뒤로가기의 자연스러운 목적지는 방 리스트이다.
        ///   타이틀로 보내면 사용자가 다시 방 리스트까지 2단계를 거쳐야 하므로 UX가 나빠진다.
        /// </summary>
        public void OnClickLeaveRoom()
        {
            CancelCountdown();

            // 캐릭터 선택 팝업이 열려 있으면 닫기
            if (characterSelectUI != null && characterSelectUI.IsOpen)
            {
                characterSelectUI.Close();
            }

            NetworkManager.Instance?.LeaveRoom();
        }

        // ===================================================================
        // 준비 상태 ↔ 캐릭터 선택 동기화 — 요구사항 [3]
        // ===================================================================

        /// <summary>
        /// 캐릭터 선택 UI의 잠금 상태를 현재 게임 상태에 맞춰 동기화한다.
        ///
        /// 잠금 조건 = 본인 Ready 상태 OR 카운트다운 활성.
        ///   - 잠금 시: 선택 버튼 비활성 + 열린 팝업 강제 닫기.
        ///   - 해제 시: 버튼 다시 활성.
        ///
        /// 왜 통합 메서드인가:
        ///   Ready와 카운트다운 둘 다 "선택을 막아야 할 상태"이므로 잠금 조건을 한 곳에서 관리한다.
        ///   호출자는 상태 변경 때마다 이 메서드를 한 번 호출하면 된다.
        /// </summary>
        private void SyncCharacterSelectLockState()
        {
            bool locked = IsLocalPlayerReady() || IsCountdownActive();

            if (characterSelectButton != null)
            {
                characterSelectButton.interactable = !locked;
            }

            if (locked && characterSelectUI != null && characterSelectUI.IsOpen)
            {
                characterSelectUI.Close();
            }
        }

        // ===================================================================
        // 이벤트 핸들러
        // ===================================================================

        private void HandlePlayersChanged()
        {
            RefreshRoomUi();
            RefreshRoleUi();

            if (NetworkManager.Instance == null) return;

            var canStart = NetworkManager.Instance.CanMasterStartGameInCurrentRoom();

            if (!PhotonNetwork.IsMasterClient) return;

            // 카운트다운 중에 누군가 준비를 풀면 즉시 취소 (안전장치).
            // 자동 시작은 더 이상 수행하지 않고, 호스트의 Start 버튼 클릭을 기다린다.
            if (IsCountdownActive() && !canStart)
            {
                SetStateText("카운트다운 취소: 준비 상태가 변경되었습니다.");
                CancelCountdown();
            }
        }

        /// <summary>
        /// 방 퇴장 완료 콜백.
        ///
        /// [1] 변경: ShowTitle() → ShowRoomList()
        /// </summary>
        private void HandleLeftRoom()
        {
            CancelCountdown();
            if (readyToggle != null)
            {
                readyToggle.isOn = false;
            }

            ShowCountdownText(string.Empty);

            // ★ [1] 방 리스트 패널로 이동 (기존: menuSceneManager?.ShowTitle())
            menuSceneManager?.ShowRoomList();
        }

        // ===================================================================
        // UI 갱신
        // ===================================================================

        private void RefreshRoleUi()
        {
            var isReady = IsLocalPlayerReady();

            if (readyToggle != null)
            {
                readyToggle.interactable = true;
                if (NetworkManager.Instance != null && PhotonNetwork.InRoom)
                {
                    readyToggle.isOn = isReady;
                }
            }

            // 같은 버튼을 역할에 따라 "시작"(호스트) / "준비"(클라)로 겸용.
            // 무엇: startButton을 항상 보이게 유지하고, 텍스트·interactable만 역할별로 조정.
            // 왜:   클라에도 준비 수단이 필요하다. 버튼을 둘로 쪼개기보다 하나의 버튼이
            //       호스트/클라에 따라 의미를 바꾸는 것이 UI 점유와 복잡도를 줄인다.
            // 어떻게: OnClickStartGame 핸들러 내부에서 IsMasterClient 분기로 실제 동작을 가른다.
            bool isHost = PhotonNetwork.IsMasterClient;

            bool countdownActive = IsCountdownActive();

            if (startButton != null)
            {
                startButton.gameObject.SetActive(true);

                if (isHost)
                {
                    // 호스트: 카운트다운 중이면 "취소" 기능으로 항상 활성.
                    // 평시엔 전원 준비되었을 때만 활성(시작 가능 조건).
                    bool canStart = NetworkManager.Instance != null
                                    && NetworkManager.Instance.CanMasterStartGameInCurrentRoom();
                    startButton.interactable = countdownActive || canStart;
                }
                else
                {
                    // 클라: 항상 준비 토글 가능 (카운트다운 중에도 취소를 위해 허용).
                    startButton.interactable = true;
                }
            }

            if (readyStartButtonText != null)
            {
                if (isHost)
                {
                    readyStartButtonText.text = countdownActive ? "취소" : "시작";
                }
                else
                {
                    readyStartButtonText.text = isReady ? "준비취소" : "준비";
                }
            }

            // 캐릭터 선택 버튼은 Ready + 카운트다운 양쪽을 모두 고려해 잠금.
            // 카운트다운이 방금 시작됐거나 취소됐을 때 OnRoomPropertiesUpdate가 RefreshRoleUi를
            // 호출하므로, 이 한 줄로 버튼 활성 상태와 팝업 열림 여부가 자동 동기화된다.
            SyncCharacterSelectLockState();
        }

        // ===================================================================
        // 헬퍼
        // ===================================================================

        /// <summary>
        /// 로컬 플레이어의 준비 상태를 조회하는 헬퍼.
        /// 여러 곳에서 반복되는 null 체크를 한곳에 모아 가독성을 높인다.
        /// </summary>
        private bool IsLocalPlayerReady()
        {
            return NetworkManager.Instance != null
                   && PhotonNetwork.InRoom
                   && NetworkManager.Instance.IsPlayerReady(PhotonNetwork.LocalPlayer);
        }

        // ===================================================================
        // 카운트다운 (기존 로직 유지)
        // ===================================================================

        private void StartCountdown()
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            {
                return;
            }

            if (IsCountdownActive())
            {
                return;
            }

            PhotonNetwork.CurrentRoom.IsOpen = false;

            var props = new Hashtable
            {
                [CountdownActiveKey] = true,
                [CountdownEndTimeKey] = PhotonNetwork.Time + startCountdownSeconds
            };

            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            displayedCountdown = -1;
        }

        private void CancelCountdown(bool resetLoadingFlag = true)
        {
            displayedCountdown = -1;
            if (resetLoadingFlag)
            {
                isLoadingGameScene = false;
            }
            ShowCountdownText(string.Empty);

            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            {
                return;
            }

            PhotonNetwork.CurrentRoom.IsOpen = true;

            var props = new Hashtable
            {
                [CountdownActiveKey] = null,
                [CountdownEndTimeKey] = null
            };

            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }

        private bool IsCountdownActive()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            {
                return false;
            }

            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CountdownActiveKey, out var value))
            {
                return false;
            }

            return value is bool active && active;
        }

        private bool TryGetCountdownEndTime(out double endTime)
        {
            endTime = 0d;
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            {
                return false;
            }

            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(CountdownEndTimeKey, out var value) || value == null)
            {
                return false;
            }

            switch (value)
            {
                case double d:
                    endTime = d;
                    return true;
                case float f:
                    endTime = f;
                    return true;
                default:
                    return double.TryParse(value.ToString(), out endTime);
            }
        }

        private void UpdateCountdownUiAndStart()
        {
            if (!IsCountdownActive())
            {
                ShowCountdownText(string.Empty);
                return;
            }

            if (!TryGetCountdownEndTime(out var endTime))
            {
                ShowCountdownText(string.Empty);
                return;
            }

            var remainSeconds = Mathf.CeilToInt((float)(endTime - PhotonNetwork.Time));
            if (remainSeconds > 0)
            {
                if (remainSeconds != displayedCountdown)
                {
                    displayedCountdown = remainSeconds;
                    ShowCountdownText(remainSeconds.ToString());
                }
                return;
            }

            ShowCountdownText(string.Empty);

            PhotonNetwork.AutomaticallySyncScene = true;

            if (!PhotonNetwork.IsMasterClient || isLoadingGameScene)
            {
                return;
            }

            if (NetworkManager.Instance == null || !NetworkManager.Instance.CanMasterStartGameInCurrentRoom())
            {
                CancelCountdown();
                return;
            }

            isLoadingGameScene = true;
            CancelCountdown(false);

            if (PhotonNetwork.CurrentRoom != null)
            {
                PhotonNetwork.CurrentRoom.IsVisible = false;
                PhotonNetwork.CurrentRoom.IsOpen = false;
            }

            TestManager.Instance?.EnterGameSceneByMaster();
        }

        private void ShowCountdownText(string text)
        {
            if (countdownText == null)
            {
                return;
            }

            countdownText.text = text;
            var shouldShow = !string.IsNullOrEmpty(text);
            if (countdownText.gameObject.activeSelf != shouldShow)
            {
                countdownText.gameObject.SetActive(shouldShow);
            }
        }

        // ===================================================================
        // 방 정보 UI (기존 로직 유지)
        // ===================================================================

        private void RefreshRoomUi()
        {
            if (roomInfoText != null)
            {
                if (PhotonNetwork.InRoom)
                {
                    roomInfoText.text =
                        $"Room: {PhotonNetwork.CurrentRoom.Name}  |  Players: {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers}";
                }
                else
                {
                    roomInfoText.text = "Not in room";
                }
            }

            RefreshLobbyEntries();
        }

        /// <summary>
        /// 플레이어 리스트 엔트리 풀을 갱신한다.
        ///
        /// 무엇: PhotonNetwork.PlayerList를 순회하며 LobbyPlayerEntry 프리팹을 인스턴스화/재사용/여분 숨김.
        /// 왜:   기존 playersStatusText는 한 덩어리 TMP라 행별 Kick 버튼을 달 수 없었다.
        /// 어떻게: 필요한 만큼 Instantiate(캐시) → 남는 건 SetActive(false).
        ///        Bind(player)가 Kick 버튼 표시/숨김까지 처리한다.
        /// </summary>
        private void RefreshLobbyEntries()
        {
            if (lobbyEntryContainer == null || lobbyEntryPrefab == null) return;
            if (!PhotonNetwork.InRoom) return;

            var players = PhotonNetwork.PlayerList;

            // 필요한 개수만큼 풀을 확장.
            while (entryPool.Count < players.Length)
            {
                var entry = Instantiate(lobbyEntryPrefab, lobbyEntryContainer);
                entryPool.Add(entry);
            }

            // 바인딩.
            for (int i = 0; i < entryPool.Count; i++)
            {
                if (i < players.Length)
                {
                    if (!entryPool[i].gameObject.activeSelf) entryPool[i].gameObject.SetActive(true);
                    entryPool[i].Bind(players[i]);
                }
                else
                {
                    entryPool[i].Clear();
                    if (entryPool[i].gameObject.activeSelf) entryPool[i].gameObject.SetActive(false);
                }
            }
        }

        private void SetStateText(string text)
        {
            if (stateText != null)
            {
                stateText.text = text;
            }
        }

        // ===================================================================
        // UI 자동 생성 (기존 로직 유지)
        // ===================================================================

        private void EnsureUiReferences()
        {
            if (roomInfoText == null)
            {
                roomInfoText = CreateOrFindText(
                    "RoomInfoText",
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(0f, -30f),
                    new Vector2(760f, 40f),
                    28,
                    TextAlignmentOptions.Center);
            }

            if (countdownText == null)
            {
                countdownText = CreateOrFindText(
                    "CountdownText",
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 10f),
                    new Vector2(240f, 180f),
                    120,
                    TextAlignmentOptions.Center);
                if (countdownText != null)
                {
                    countdownText.gameObject.SetActive(false);
                }
            }

            if (stateText == null)
            {
                stateText = CreateOrFindText(
                    "StateText",
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(0f, 30f),
                    new Vector2(760f, 40f),
                    24,
                    TextAlignmentOptions.Center);
            }
        }

        private TMP_Text CreateOrFindText(
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPos,
            Vector2 size,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            var child = transform.Find(objectName);
            if (child != null)
            {
                var existing = child.GetComponent<TMP_Text>();
                if (existing != null)
                {
                    return existing;
                }
            }

            var go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            rect.pivot = new Vector2(0.5f, 0.5f);

            var textUi = go.GetComponent<TextMeshProUGUI>();
            textUi.fontSize = fontSize;
            textUi.alignment = alignment;
            textUi.color = Color.white;
            textUi.raycastTarget = false;
            textUi.text = string.Empty;
            return textUi;
        }
    }
}
