using System;
using SwDreams.Features.UI.Adapter.Menu;
using SwDreams.Features.UI.Presentation;
using SwDreams.Shared.Managers;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SwDreams.Features.UI.Adapter.Menu
{
    /// <summary>
    /// 방 리스트 패널의 전체 흐름을 관리하는 Controller.
    ///
    /// 책임(SRP):
    ///   - NetworkManager 이벤트 구독/해제
    ///   - 방 생성/참가 요청 중계
    ///   - 비밀번호 팝업 흐름 관리
    ///   - 새로고침 버튼 쿨다운 제어
    ///   - UI 아이템의 실제 생성/재활용은 RoomListView에 위임
    ///
    /// 새로고침 정책:
    ///   Photon PUN2는 로비에 있으면 서버가 방 목록 변경을 자동 푸시한다.
    ///   따라서 수동 새로고침 버튼은 현재 캐시된 목록으로 UI를 다시 그리는 역할이며,
    ///   연타 방지를 위한 쿨다운만 적용한다.
    /// </summary>
    public class RoomListPanelController : MonoBehaviour, IRoomListItemHandler
    {
        [SerializeField] private MenuSceneManager menuSceneManager;
        [SerializeField] private string defaultRoomName = "Room_0001";

        [Header("Create Room UI")]
        [SerializeField] private GameObject makeRoomPanel;
        [SerializeField] private TMP_InputField roomNameInputField;
        [SerializeField] private TMP_InputField createRoomPasswordInputField;

        [Header("Create Room — Password Toggle")]
        [Tooltip("비밀번호 사용 여부 Toggle. OnValueChanged(Boolean)에 OnTogglePasswordUse 연결. 체크 아이콘 스왑은 Toggle 자체 기능 사용.")]
        [SerializeField] private Toggle usePasswordToggle;
        [Tooltip("비밀번호 미사용 시 보일 'Off' 오브젝트 (회색/플레이스홀더 등).")]
        [SerializeField] private GameObject passwordOffView;
        [Tooltip("비밀번호 사용 시 보일 InputField 루트 오브젝트.")]
        [SerializeField] private GameObject passwordInputView;

        private bool passwordUseOn;

        [Header("Search Room Popup")]
        [SerializeField] private GameObject searchRoomPopup;
        [SerializeField] private TMP_InputField searchRoomInputField;

        [Header("Join Room UI")]
        [SerializeField] private TMP_InputField joinRoomPasswordInputField;
        [SerializeField] private GameObject joinPasswordPopup;
        [SerializeField] private TMP_InputField joinPasswordPopupInputField;

        [Header("Display")]
        [SerializeField] private RoomListView roomListView;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text emptyListText;

        [Header("Refresh")]
        [SerializeField] private Button refreshButton;
        [Tooltip("새로고침 버튼 연타 방지 쿨다운 (초)")]
        [SerializeField] private float refreshCooldown = 2f;

        private string pendingJoinRoomName = string.Empty;
        private float refreshCooldownTimer;

        private void OnEnable()
        {
            if (NetworkManager.Instance == null)
            {
                return;
            }

            NetworkManager.Instance.JoinedRoom += HandleJoinedRoom;
            NetworkManager.Instance.JoinRoomFailed += HandleJoinRoomFailed;
            NetworkManager.Instance.CreateRoomFailed += HandleCreateRoomFailed;
            NetworkManager.Instance.RoomListChanged += HandleRoomListChanged;

            SetCreateRoomPanel(false);
            SetJoinPasswordPopup(false);
            SetSearchRoomPopup(false);
            ClearAllInputFields();

            refreshCooldownTimer = 0f;

            HandleRoomListChanged();
            UpdateRefreshButtonState();
        }

        private void OnDisable()
        {
            if (NetworkManager.Instance == null)
            {
                return;
            }

            NetworkManager.Instance.JoinedRoom -= HandleJoinedRoom;
            NetworkManager.Instance.JoinRoomFailed -= HandleJoinRoomFailed;
            NetworkManager.Instance.CreateRoomFailed -= HandleCreateRoomFailed;
            NetworkManager.Instance.RoomListChanged -= HandleRoomListChanged;

            if (roomListView != null)
            {
                roomListView.ClearAll();
            }
        }

        private void Update()
        {
            // 쿨다운 타이머 감소 및 버튼 상태 갱신
            if (refreshCooldownTimer > 0f)
            {
                refreshCooldownTimer -= Time.unscaledDeltaTime;
                if (refreshCooldownTimer <= 0f)
                {
                    refreshCooldownTimer = 0f;
                    UpdateRefreshButtonState();
                }
            }
        }

        // ===== IRoomListItemHandler 구현 =====

        public void OnRoomItemClicked(RoomInfo roomInfo)
        {
            if (roomInfo == null)
            {
                return;
            }

            TryJoinRoom(roomInfo.Name);
        }

        // ===== 버튼 핸들러 =====

        /// <summary>
        /// 새로고침 버튼 클릭.
        /// Photon이 로비에서 방 목록을 자동 푸시하므로,
        /// 수동 버튼은 현재 캐시된 목록으로 UI를 다시 그리는 역할.
        /// 연타 방지를 위해 쿨다운을 적용한다.
        /// </summary>
        public void OnClickRefreshRoomList()
        {
            if (refreshCooldownTimer > 0f)
            {
                return;
            }

            refreshCooldownTimer = refreshCooldown;
            UpdateRefreshButtonState();

            HandleRoomListChanged();
            FrameToastController.Show("방 목록을 새로고침했습니다", duration: 1.5f);
        }

        public void OnClickOpenCreateRoomPopup()
        {
            SetCreateRoomPanel(true);

            // 팝업 열 때 비밀번호 상태 초기화 (기본: 비밀번호 없음).
            // Toggle.isOn 재설정이 false→false면 OnValueChanged가 발화되지 않으므로
            // ApplyPasswordUseState를 명시적으로 호출해 뷰를 확실히 동기화한다.
            if (usePasswordToggle != null)
            {
                usePasswordToggle.SetIsOnWithoutNotify(false);
            }
            ApplyPasswordUseState(false);
        }

        public void OnClickCreateRoom()
        {
            OnClickOpenCreateRoomPopup();
        }

        public void OnClickCloseCreateRoomPopup()
        {
            SetCreateRoomPanel(false);
            ClearCreateRoomInputFields();
        }

        /// <summary>
        /// 비밀번호 사용 Toggle의 OnValueChanged(bool) 핸들러.
        ///
        /// 무엇: 체크 상태에 따라 Image sprite를 스왑하고 Off/InputField 뷰를 전환한다.
        /// 왜:   UI가 Toggle 컴포넌트 + 단일 Image sprite 교체 구조로 구성됨.
        ///       Toggle이 체크 상태를 자체 보존하므로 내부 bool도 함께 동기화해 기록용으로 남긴다.
        /// 어떻게: Toggle.onValueChanged(Boolean) Inspector 이벤트에 이 메서드를 연결.
        /// </summary>
        public void OnTogglePasswordUse(bool useOn)
        {
            ApplyPasswordUseState(useOn);
        }

        private void ApplyPasswordUseState(bool useOn)
        {
            passwordUseOn = useOn;

            // Off 뷰 / InputField 뷰 스왑
            // (체크 이미지 스왑은 Toggle 자체 기능 사용 — 여기서는 처리하지 않음)
            if (passwordOffView != null) passwordOffView.SetActive(!useOn);
            if (passwordInputView != null) passwordInputView.SetActive(useOn);

            // 비밀번호 미사용으로 돌아가면 입력 내용 초기화 (잔존 텍스트로 인한 사고 방지)
            if (!useOn && createRoomPasswordInputField != null)
            {
                createRoomPasswordInputField.text = string.Empty;
            }
        }

        public void OnClickConfirmCreateRoom()
        {
            if (NetworkManager.Instance == null)
            {
                return;
            }

            var roomName = ReadCreateRoomNameOrDefault();
            if (IsDuplicateRoomName(roomName))
            {
                FrameToastController.Show("이미 존재하는 방 이름입니다");
                return;
            }

            // 체크박스가 꺼져 있으면 입력창 텍스트가 남아 있어도 무시하고 빈 값으로 강제.
            // → "체크 안 하면 비밀번호 없는 방"이라는 UX 약속을 데이터 레벨에서 보장.
            var password = passwordUseOn && createRoomPasswordInputField != null
                ? createRoomPasswordInputField.text
                : string.Empty;

            NetworkManager.Instance.CreateRoom(roomName, password);

            FrameToastController.Show(
                string.IsNullOrWhiteSpace(password) ? $"방 생성 중: {roomName}" : $"방 생성 중: {roomName} (비밀번호)",
                duration: 1.5f);
        }

        /// <summary>
        /// 방 찾기 버튼 클릭 → 검색 팝업 열기.
        /// </summary>
        public void OnClickOpenSearchRoomPopup()
        {
            SetSearchRoomPopup(true);
        }

        /// <summary>
        /// 검색 팝업의 CloseBtn 클릭.
        /// </summary>
        public void OnClickCloseSearchRoomPopup()
        {
            SetSearchRoomPopup(false);
        }

        /// <summary>
        /// 검색 팝업의 Search 버튼 클릭.
        /// 입력된 방 이름으로 진입을 시도한다.
        /// 비밀번호 방이면 비밀번호 팝업으로 넘어간다.
        /// </summary>
        public void OnClickSearchRoom()
        {
            var roomName = ReadSearchRoomName();
            if (string.IsNullOrWhiteSpace(roomName))
            {
                FrameToastController.Show("방 이름을 입력하세요");
                return;
            }

            // 검색 팝업은 진입 성공 시 HandleJoinedRoom 에서 닫는다.
            // 실패(존재하지 않는 방) 시에는 입력 유지로 재시도 편의 제공.
            // 잠긴 방으로 분기되어 비밀번호 팝업이 열릴 때는 TryJoinRoom 안에서 검색 팝업을 닫는다.
            TryJoinRoom(roomName);
        }

        public void OnClickJoinRoomFromInput()
        {
            var roomName = ReadSearchRoomName();
            TryJoinRoom(roomName);
        }

        public void OnClickJoinRoom(string roomCode)
        {
            TryJoinRoom(roomCode);
        }

        public void OnClickSearchChanged()
        {
            HandleRoomListChanged();
        }

        public void OnClickBack()
        {
            ClearAllInputFields();
            menuSceneManager?.ShowTitle();
        }

        public void OnClickConfirmJoinPassword()
        {
            if (string.IsNullOrWhiteSpace(pendingJoinRoomName))
            {
                SetJoinPasswordPopup(false);
                FrameToastController.Show("선택된 방이 없습니다");
                return;
            }

            var password = joinPasswordPopupInputField != null ? joinPasswordPopupInputField.text : string.Empty;
            var targetRoom = FindRoomByName(pendingJoinRoomName);
            if (targetRoom == null)
            {
                SetJoinPasswordPopup(false);
                FrameToastController.Show("존재하지 않는 방입니다");
                return;
            }

            // 사전 검증으로 JoinRoom 자체를 건너뛴다 → 호스트 화면에 시도자가 보이지 않음.
            // 불일치 시 팝업은 유지해 재입력 편의 제공.
            if (!NetworkManager.Instance.IsRoomPasswordMatch(targetRoom, password))
            {
                FrameToastController.Show("비밀번호가 일치하지 않습니다");
                return;
            }

            NetworkManager.Instance.JoinRoom(pendingJoinRoomName, password);
            FrameToastController.Show($"방 입장 중: {pendingJoinRoomName}", duration: 1.5f);
            SetJoinPasswordPopup(false);
        }

        public void OnClickCancelJoinPassword()
        {
            pendingJoinRoomName = string.Empty;
            SetJoinPasswordPopup(false);
        }

        // ===== 새로고침 =====

        /// <summary>
        /// 쿨다운 타이머에 따라 버튼 interactable을 토글.
        /// 쿨다운 중이면 비활성화, 끝나면 활성화.
        /// </summary>
        private void UpdateRefreshButtonState()
        {
            if (refreshButton == null)
            {
                return;
            }

            refreshButton.interactable = refreshCooldownTimer <= 0f;
        }

        // ===== 내부 로직 =====

        private string ReadCreateRoomNameOrDefault()
        {
            var raw = roomNameInputField != null ? roomNameInputField.text : string.Empty;
            if (!string.IsNullOrWhiteSpace(raw))
            {
                return raw.Trim();
            }
            // 빈 입력이면 defaultRoomName 의 prefix 를 이어받아 다음 사용 가능한 번호 부여.
            // 예) defaultRoomName="Room_0001" → 캐시에 없는 첫 번째 "Room_NNNN" 반환.
            return FindAvailableDefaultRoomName();
        }

        /// <summary>
        /// defaultRoomName 에서 끝 숫자를 떼 prefix 를 얻고, 캐시와 충돌 없는 첫 번째 4자리 번호를 붙여 반환.
        /// </summary>
        private string FindAvailableDefaultRoomName()
        {
            var prefix = ExtractPrefix(defaultRoomName);
            for (var i = 1; i < 10000; i++)
            {
                var candidate = $"{prefix}{i:D4}";
                if (!IsDuplicateRoomName(candidate)) return candidate;
            }
            // 1~9999 모두 충돌하는 극단 케이스 — 랜덤 fallback.
            return $"{prefix}{UnityEngine.Random.Range(0, 10000):D4}";
        }

        /// <summary>
        /// 문자열 끝에 붙은 숫자를 제거해 prefix 를 추출. "Room_0001" → "Room_". 숫자가 없으면 "_" 를 덧붙인다.
        /// </summary>
        private static string ExtractPrefix(string defaultName)
        {
            if (string.IsNullOrEmpty(defaultName)) return "Room_";
            var i = defaultName.Length;
            while (i > 0 && char.IsDigit(defaultName[i - 1])) i--;
            return i == defaultName.Length ? defaultName + "_" : defaultName.Substring(0, i);
        }

        private string ReadSearchRoomName()
        {
            var raw = searchRoomInputField != null ? searchRoomInputField.text : string.Empty;
            return string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim();
        }

        private bool IsDuplicateRoomName(string roomName)
        {
            if (NetworkManager.Instance == null || string.IsNullOrWhiteSpace(roomName))
            {
                return false;
            }

            var rooms = NetworkManager.Instance.CachedRoomList;
            for (var i = 0; i < rooms.Length; i++)
            {
                if (string.Equals(rooms[i].Name, roomName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void TryJoinRoom(string roomName)
        {
            if (NetworkManager.Instance == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(roomName))
            {
                FrameToastController.Show("방 이름을 입력하세요");
                return;
            }

            var targetRoom = FindRoomByName(roomName);
            if (targetRoom == null)
            {
                FrameToastController.Show("존재하지 않는 방입니다");
                return;
            }

            var isProtected = NetworkManager.Instance.IsRoomPasswordProtected(targetRoom);
            if (!isProtected)
            {
                NetworkManager.Instance.JoinRoom(targetRoom.Name);
                FrameToastController.Show($"방 입장 중: {targetRoom.Name}", duration: 1.5f);
                return;
            }

            if (joinPasswordPopup != null && joinPasswordPopupInputField != null)
            {
                pendingJoinRoomName = targetRoom.Name;
                joinPasswordPopupInputField.text = string.Empty;
                // 비밀번호 팝업이 열릴 때는 검색 팝업을 함께 닫는다 (UX: 동시 노출 방지).
                SetSearchRoomPopup(false);
                SetJoinPasswordPopup(true);
                return;
            }

            var inlinePassword = joinRoomPasswordInputField != null ? joinRoomPasswordInputField.text : string.Empty;
            if (string.IsNullOrWhiteSpace(inlinePassword))
            {
                FrameToastController.Show("비밀번호가 필요한 방입니다");
                return;
            }

            // 사전 검증 — 비번 팝업 없는 inline 입력 경로에서도 호스트 화면 깜빡임 회피.
            if (!NetworkManager.Instance.IsRoomPasswordMatch(targetRoom, inlinePassword))
            {
                FrameToastController.Show("비밀번호가 일치하지 않습니다");
                return;
            }

            NetworkManager.Instance.JoinRoom(targetRoom.Name, inlinePassword);
            FrameToastController.Show($"방 입장 중: {targetRoom.Name}", duration: 1.5f);
        }

        private RoomInfo FindRoomByName(string roomName)
        {
            if (NetworkManager.Instance == null)
            {
                return null;
            }

            var rooms = NetworkManager.Instance.CachedRoomList;
            for (var i = 0; i < rooms.Length; i++)
            {
                if (string.Equals(rooms[i].Name, roomName, StringComparison.OrdinalIgnoreCase))
                {
                    return rooms[i];
                }
            }

            return null;
        }

        // ===== 이벤트 핸들러 =====

        private void HandleJoinedRoom()
        {
            SetCreateRoomPanel(false);
            SetJoinPasswordPopup(false);
            SetSearchRoomPopup(false);
            ClearAllInputFields();
            menuSceneManager?.ShowWaitingRoom();
        }

        // 입장/생성 실패 토스트는 MenuSceneManager 가 NetworkManager 이벤트를 직접 구독해 처리한다.
        // RoomListPanelController 는 활성 상태에서만 구독 살아있어 비밀번호 검증 분기 등에서
        // 토스트가 누락될 수 있어 글로벌 핸들러로 일원화.
        private void HandleJoinRoomFailed(short returnCode, string message)
        {
            Debug.LogWarning($"Join room failed ({returnCode}): {message}");
        }

        private void HandleCreateRoomFailed(short returnCode, string message)
        {
            Debug.LogWarning($"Create room failed ({returnCode}): {message}");
        }

        private void HandleRoomListChanged()
        {
            if (roomListView == null)
            {
                return;
            }

            if (NetworkManager.Instance == null)
            {
                roomListView.ClearAll();
                SetEmptyListText("NetworkManager not found.");
                return;
            }

            var rooms = NetworkManager.Instance.CachedRoomList;

            roomListView.SyncItems(
                rooms,
                handler: this,
                isPasswordProtected: room => NetworkManager.Instance.IsRoomPasswordProtected(room),
                filter: null);

            SetEmptyListText(roomListView.ActiveItemCount == 0 ? "No rooms available." : string.Empty);
        }

        // ===== UI 헬퍼 =====

        private void SetCreateRoomPanel(bool active)
        {
            if (makeRoomPanel != null)
            {
                makeRoomPanel.SetActive(active);
            }
        }

        private void SetJoinPasswordPopup(bool active)
        {
            if (joinPasswordPopup != null)
            {
                joinPasswordPopup.SetActive(active);
            }
        }

        private void SetSearchRoomPopup(bool active)
        {
            if (searchRoomPopup != null)
            {
                searchRoomPopup.SetActive(active);
            }

            // 팝업 닫을 때 입력 필드 초기화
            if (!active && searchRoomInputField != null)
            {
                searchRoomInputField.text = string.Empty;
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        private void SetEmptyListText(string message)
        {
            if (emptyListText != null)
            {
                emptyListText.text = message;
                emptyListText.gameObject.SetActive(!string.IsNullOrEmpty(message));
            }
        }

        /// <summary>
        /// 모든 입력 필드를 초기화.
        /// 호출 시점: OnEnable(패널 열림), HandleJoinedRoom(방 진입), OnClickBack(뒤로가기)
        /// </summary>
        private void ClearAllInputFields()
        {
            ClearCreateRoomInputFields();

            if (searchRoomInputField != null)
            {
                searchRoomInputField.text = string.Empty;
            }

            if (joinRoomPasswordInputField != null)
            {
                joinRoomPasswordInputField.text = string.Empty;
            }

            if (joinPasswordPopupInputField != null)
            {
                joinPasswordPopupInputField.text = string.Empty;
            }

            pendingJoinRoomName = string.Empty;
        }

        /// <summary>
        /// 방 생성 팝업의 입력 필드만 초기화.
        /// 호출 시점: OnClickCloseCreateRoomPopup(닫기 버튼)
        /// </summary>
        private void ClearCreateRoomInputFields()
        {
            if (roomNameInputField != null)
            {
                roomNameInputField.text = string.Empty;
            }

            if (createRoomPasswordInputField != null)
            {
                createRoomPasswordInputField.text = string.Empty;
            }

            // 체크박스도 초기 상태(비밀번호 없음)로 리셋.
            if (usePasswordToggle != null)
            {
                usePasswordToggle.SetIsOnWithoutNotify(false);
            }
            ApplyPasswordUseState(false);
        }
    }
}
