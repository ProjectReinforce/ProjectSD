using System.Text;
using SwDreams.Features.UI.Adapter.Menu;
using SwDreams.Features.Character.Adapter.Data;
using SwDreams.Shared.Managers;
using ExitGames.Client.Photon;
using Photon.Pun;
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
        [SerializeField] private TMP_Text playersStatusText;
        [SerializeField] private TMP_Text countdownText;
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private Toggle readyToggle;
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text readyStartButtonText;

        [Header("캐릭터 선택")]
        [Tooltip("캐릭터 셀렉트 팝업을 여는 버튼")]
        [SerializeField] private Button characterSelectButton;
        [Tooltip("CharacterSelectUI가 부착된 팝업 패널")]
        [SerializeField] private CharacterSelectUI characterSelectUI;

        private int displayedCountdown = -1;
        private bool isLoadingGameScene;

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

            // 캐릭터 셀렉트 버튼 리스너 등록
            if (characterSelectButton != null)
            {
                characterSelectButton.onClick.AddListener(OnClickCharacterSelect);
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
        /// 캐릭터 셀렉트 버튼 클릭 핸들러.
        /// 준비 상태가 아닐 때만 팝업을 연다.
        ///
        /// 왜 interactable 대신 코드 가드도 넣는가:
        ///   interactable = false 상태에서도 코드에서 onClick.Invoke()를 호출할 수 있다.
        ///   방어적 프로그래밍으로 양쪽 모두 막는 것이 안전하다.
        /// </summary>
        private void OnClickCharacterSelect()
        {
            // [3] 준비 상태에서는 선택 불가
            if (IsLocalPlayerReady())
            {
                SetStateText("준비 상태에서는 캐릭터를 변경할 수 없습니다.");
                return;
            }

            if (characterSelectUI == null)
            {
                Debug.LogWarning("[WaitingRoom] CharacterSelectUI 참조가 없습니다.");
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

            // [3] 준비 상태 변경 시 캐릭터 선택 UI 동기화
            SyncCharacterSelectWithReadyState(ready);

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

        public void OnClickStartGame()
        {
            OnClickReadyOrStart();
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
        /// 준비 상태가 변경될 때 캐릭터 선택 관련 UI를 동기화한다.
        ///
        /// 왜 이 로직을 별도 메서드로 분리하는가:
        ///   OnToggleReady()는 "준비 상태를 네트워크에 전파"하는 책임이고,
        ///   이 메서드는 "준비 상태에 따라 UI를 제어"하는 책임이다.
        ///   SRP에 따라 분리하면 각각 독립적으로 변경 가능하다.
        ///   (예: 나중에 장비 선택 UI가 추가되어도 여기에 한 줄만 추가하면 됨)
        /// </summary>
        private void SyncCharacterSelectWithReadyState(bool isReady)
        {
            // 셀렉트 버튼 interactable 제어
            if (characterSelectButton != null)
            {
                characterSelectButton.interactable = !isReady;
            }

            // 준비 상태 진입 시 열려 있는 선택 팝업 강제 닫기
            if (isReady && characterSelectUI != null && characterSelectUI.IsOpen)
            {
                characterSelectUI.Close();
                SetStateText("준비 완료 — 캐릭터 선택창이 닫혔습니다.");
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

            if (canStart && !IsCountdownActive())
            {
                SetStateText("전원 준비 완료. 카운트다운 시작...");
                StartCountdown();
                return;
            }

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

            if (startButton != null)
            {
                startButton.gameObject.SetActive(true);
                startButton.interactable = PhotonNetwork.InRoom;
            }

            if (readyStartButtonText != null)
            {
                readyStartButtonText.text = isReady ? "준비취소" : "준비";
            }

            // [3] 준비 상태에 따라 캐릭터 셀렉트 버튼 interactable 동기화
            // RefreshRoleUi()는 다른 플레이어의 상태 변경에도 호출되므로,
            // 여기서도 로컬 플레이어의 준비 상태를 기준으로 버튼을 제어한다.
            if (characterSelectButton != null)
            {
                characterSelectButton.interactable = !isReady;
            }
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

            if (playersStatusText == null || !PhotonNetwork.InRoom || NetworkManager.Instance == null)
            {
                return;
            }

            var sb = new StringBuilder();
            var players = PhotonNetwork.PlayerList;
            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                var isYou = player.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber;
                var role = player.IsMasterClient ? "Host" : "Client";
                var ready = NetworkManager.Instance.IsPlayerReady(player) ? "준비" : "대기";
                var character = NetworkManager.Instance.TryGetCharacterId(player, out var id) ? id.ToString() : "-";

                sb.Append("P")
                    .Append(player.ActorNumber)
                    .Append(isYou ? " (You)" : string.Empty)
                    .Append(" | ")
                    .Append(role)
                    .Append(" | Char: ")
                    .Append(character)
                    .Append(" | ")
                    .AppendLine(ready);
            }

            playersStatusText.text = sb.ToString();
        }

        private void SetStateText(string text)
        {
            if (stateText != null)
            {
                stateText.text = text;
            }
        }

        private void TryStartByHost()
        {
            OnClickReadyOrStart();
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

            if (playersStatusText == null)
            {
                playersStatusText = CreateOrFindText(
                    "PlayersStatusText",
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(260f, -140f),
                    new Vector2(520f, 280f),
                    24,
                    TextAlignmentOptions.TopLeft);
            }
            ApplyPlayersStatusLayout(playersStatusText);

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

        private void ApplyPlayersStatusLayout(TMP_Text target)
        {
            if (target == null)
            {
                return;
            }

            var rect = target.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(260f, -140f);
            rect.sizeDelta = new Vector2(520f, 280f);
        }
    }
}
