using System;
using Adapter.Manager;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Adapter.UI.Menu
{
    /// <summary>
    /// 방 리스트 패널의 전체 흐름을 관리하는 Controller.
    ///
    /// 책임(SRP):
    ///   - NetworkManager 이벤트 구독/해제
    ///   - 방 생성/참가 요청 중계
    ///   - 비밀번호 팝업 흐름 관리
    ///   - 새로고침 버튼 쿨다운 제어
    ///   - 방 생성 옵션(인원수) 관리
    ///   - UI 아이템의 실제 생성/재활용은 RoomListView에 위임
    ///
    /// 새로고침 정책:
    ///   Photon PUN2는 로비에 있으면 서버가 방 목록 변경을 자동 푸시한다.
    ///   따라서 수동 새로고침 버튼은 현재 캐시된 목록으로 UI를 다시 그리는 역할이며,
    ///   연타 방지를 위한 쿨다운만 적용한다.
    ///
    /// 변경 이력:
    ///   [1] 인원수 선택 기능 추가 — playerCountToggles 배열로 1~4인 선택,
    ///       선택값을 NetworkManager.CreateRoom()에 전달하여 MaxPlayers 반영.
    /// </summary>
    public class RoomListPanelController : MonoBehaviour, IRoomListItemHandler
    {
        [SerializeField] private MenuSceneManager menuSceneManager;
        [SerializeField] private string defaultRoomName = "Room_0001";

        [Header("Create Room UI")]
        [SerializeField] private GameObject makeRoomPanel;
        [SerializeField] private TMP_InputField roomNameInputField;

        [Header("Create Room — Password")]
        [Tooltip("비밀번호 InputField. 체크박스 OFF 시 게임오브젝트 비활성화.")]
        [SerializeField] private TMP_InputField createRoomPasswordInputField;
        [Tooltip("비밀번호 활성화 체크박스 버튼 (Btn_Check). 클릭 시 비밀번호 입력 토글.")]
        [SerializeField] private Button createRoomPasswordCheckButton;
        [Tooltip("체크 활성 비주얼 (Btn_Check/On). 비밀번호 활성 시 보이고, 비활성 시 숨긴다.")]
        [SerializeField] private GameObject passwordCheckOnVisual;
        [Tooltip("체크 해제 비주얼 (Btn_Check/Off). 비밀번호 활성 시 숨기고, 비활성 시 보인다.")]
        [SerializeField] private GameObject passwordCheckOffVisual;

        [Header("Create Room — Player Count")]
        [Tooltip("인원수 선택 토글 배열. 인덱스 0 = 1인, 인덱스 3 = 4인. Inspector에서 순서대로 연결.")]
        [SerializeField] private Toggle[] playerCountToggles;
        [Tooltip("ToggleGroup 컴포넌트. 단일 선택을 보장한다. 토글들의 부모 또는 별도 오브젝트에 부착.")]
        [SerializeField] private ToggleGroup playerCountToggleGroup;
        [Tooltip("인원수 선택 토글의 기본값 (팝업 열릴 때 초기 선택)")]
        [SerializeField] private byte defaultMaxPlayers = 4;

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

        /// <summary>
        /// 방 생성 팝업에서 선택된 최대 인원수.
        /// 팝업이 열릴 때 defaultMaxPlayers로 초기화되고,
        /// 인원수 버튼 클릭 시 갱신된다.
        ///
        /// 왜 byte인가:
        ///   Photon RoomOptions.MaxPlayers가 byte 타입이므로 형변환 없이 그대로 전달.
        /// </summary>
        private byte selectedMaxPlayers;

        /// <summary>
        /// 비밀번호 체크박스의 현재 상태.
        /// false = 비밀번호 비활성 (InputField 잠김), true = 비밀번호 활성 (입력 가능).
        /// </summary>
        private bool isPasswordEnabled;

        // ===== 라이프사이클 =====

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
            ResetCreateRoomOptions();

            refreshCooldownTimer = 0f;

            HandleRoomListChanged();
            UpdateRefreshButtonState();
            SetStatus("Connected. Search, create, or join a room.");
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

            UnbindPlayerCountToggles();
            UnbindPasswordCheckButton();

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
            SetStatus("Room list refreshed.");
        }

        public void OnClickOpenCreateRoomPopup()
        {
            ResetCreateRoomOptions();
            SetCreateRoomPanel(true);
            SetStatus("Enter room options.");
        }

        public void OnClickCreateRoom()
        {
            OnClickOpenCreateRoomPopup();
        }

        public void OnClickCloseCreateRoomPopup()
        {
            SetCreateRoomPanel(false);
            ResetCreateRoomOptions();
        }

        /// <summary>
        /// 방 만들기 확인 버튼 클릭.
        ///
        /// [1] 변경: selectedMaxPlayers를 NetworkManager.CreateRoom()에 전달.
        ///     이전에는 NetworkManager 내부의 maxPlayersPerRoom(하드코딩 4)만 사용했으나,
        ///     이제 UI에서 선택한 인원수가 방의 MaxPlayers에 반영된다.
        /// </summary>
        public void OnClickConfirmCreateRoom()
        {
            if (NetworkManager.Instance == null)
            {
                return;
            }

            var roomName = ReadCreateRoomNameOrDefault();
            if (IsDuplicateRoomName(roomName))
            {
                SetStatus($"Room name already exists: {roomName}");
                return;
            }

            var password = createRoomPasswordInputField != null ? createRoomPasswordInputField.text : string.Empty;

            // ★ [1] 선택된 인원수를 전달
            NetworkManager.Instance.CreateRoom(roomName, password, selectedMaxPlayers);

            SetStatus(string.IsNullOrWhiteSpace(password)
                ? $"Creating room: {roomName} ({selectedMaxPlayers}P)"
                : $"Creating room: {roomName} ({selectedMaxPlayers}P, password)");
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
                SetStatus("Enter a room name to search.");
                return;
            }

            // 검색 팝업 닫고 진입 시도
            SetSearchRoomPopup(false);
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
                SetStatus("No room selected.");
                return;
            }

            var password = joinPasswordPopupInputField != null ? joinPasswordPopupInputField.text : string.Empty;
            NetworkManager.Instance?.JoinRoom(pendingJoinRoomName, password);
            SetStatus($"Joining room: {pendingJoinRoomName}");
            SetJoinPasswordPopup(false);
        }

        public void OnClickCancelJoinPassword()
        {
            pendingJoinRoomName = string.Empty;
            SetJoinPasswordPopup(false);
        }

        // ===== 인원수 선택 — 요구사항 [1] =====

        /// <summary>
        /// 인원수 토글 이벤트 바인딩.
        ///
        /// 왜 Toggle[] + ToggleGroup인가:
        ///   UI가 이미 토글로 구현되어 있고, ToggleGroup이 "단일 선택" 제약을
        ///   Unity 엔진 레벨에서 보장한다. 코드에서 상호 배제 로직을 직접 구현할
        ///   필요가 없으므로 버그 가능성이 줄어든다.
        ///
        /// 배열 인덱스 ↔ 인원수 매핑:
        ///   인덱스 0 → 1인, 인덱스 1 → 2인, ..., 인덱스 3 → 4인
        ///   추후 5인 이상 지원 시 배열에 토글만 추가하면 코드 변경 불필요 (OCP).
        ///
        /// ToggleGroup 세팅 (Inspector):
        ///   1. 토글들의 공통 부모(또는 별도 빈 오브젝트)에 ToggleGroup 컴포넌트 추가
        ///   2. 각 Toggle의 Group 필드에 해당 ToggleGroup 연결
        ///   3. allowSwitchOff = false (항상 하나는 선택된 상태 유지)
        /// </summary>
        private void BindPlayerCountToggles()
        {
            if (playerCountToggles == null)
            {
                return;
            }

            for (var i = 0; i < playerCountToggles.Length; i++)
            {
                if (playerCountToggles[i] == null)
                {
                    continue;
                }

                // 클로저가 올바른 값을 캡처하도록 로컬 변수 사용
                var playerCount = (byte)(i + 1);
                playerCountToggles[i].onValueChanged.AddListener(isOn => OnPlayerCountToggleChanged(playerCount, isOn));
            }
        }

        /// <summary>
        /// 인원수 토글 이벤트 해제.
        /// OnDisable에서 호출하여 리스너 누적을 방지한다.
        /// </summary>
        private void UnbindPlayerCountToggles()
        {
            if (playerCountToggles == null)
            {
                return;
            }

            for (var i = 0; i < playerCountToggles.Length; i++)
            {
                if (playerCountToggles[i] != null)
                {
                    playerCountToggles[i].onValueChanged.RemoveAllListeners();
                }
            }
        }

        /// <summary>
        /// 인원수 토글 값 변경 콜백.
        ///
        /// ToggleGroup 특성상 하나를 켜면 기존 것이 꺼지면서
        /// onValueChanged가 2번 호출된다 (기존 OFF + 새 ON).
        /// isOn == true인 경우에만 selectedMaxPlayers를 갱신하여
        /// OFF 콜백에서 불필요한 갱신을 방지한다.
        /// </summary>
        private void OnPlayerCountToggleChanged(byte count, bool isOn)
        {
            if (!isOn)
            {
                return;
            }

            selectedMaxPlayers = count;
        }

        /// <summary>
        /// 인원수 토글의 선택 상태를 코드에서 강제 설정.
        /// ResetCreateRoomOptions()에서 기본값 복원 시 호출한다.
        ///
        /// ToggleGroup이 allowSwitchOff = false일 때,
        /// SetIsOnWithoutNotify()를 사용하면 리스너를 트리거하지 않으므로
        /// 초기화 시 불필요한 콜백 호출을 피할 수 있다.
        /// </summary>
        private void SetPlayerCountToggleWithoutNotify(byte count)
        {
            if (playerCountToggles == null)
            {
                return;
            }

            for (var i = 0; i < playerCountToggles.Length; i++)
            {
                if (playerCountToggles[i] == null)
                {
                    continue;
                }

                playerCountToggles[i].SetIsOnWithoutNotify((i + 1) == count);
            }
        }

        // ===== 비밀번호 체크박스 — 요구사항 [2] =====

        /// <summary>
        /// 비밀번호 체크박스 버튼 리스너 등록.
        /// </summary>
        private void BindPasswordCheckButton()
        {
            if (createRoomPasswordCheckButton != null)
            {
                createRoomPasswordCheckButton.onClick.AddListener(OnClickPasswordCheck);
            }
        }

        /// <summary>
        /// 비밀번호 체크박스 버튼 리스너 해제.
        /// </summary>
        private void UnbindPasswordCheckButton()
        {
            if (createRoomPasswordCheckButton != null)
            {
                createRoomPasswordCheckButton.onClick.RemoveListener(OnClickPasswordCheck);
            }
        }

        /// <summary>
        /// 비밀번호 체크박스 클릭 콜백.
        ///
        /// 클릭할 때마다 isPasswordEnabled를 반전시키고 InputField 상태를 동기화한다.
        /// Button이므로 on/off 상태를 코드에서 직접 관리한다.
        ///
        /// 왜 Toggle이 아닌 Button인가:
        ///   Btn_Check 오브젝트에 이미 Button 컴포넌트가 붙어있고,
        ///   체크 비주얼(체크마크 이미지)도 자체적으로 관리하는 구조이므로
        ///   Toggle로 교체하면 기존 UI 프리팹을 변경해야 한다.
        ///   Button + bool 플래그로 동일한 동작을 구현할 수 있으므로
        ///   기존 UI 구조를 그대로 유지한다.
        /// </summary>
        private void OnClickPasswordCheck()
        {
            isPasswordEnabled = !isPasswordEnabled;
            ApplyPasswordState();
        }

        /// <summary>
        /// isPasswordEnabled 상태에 따라 InputField와 체크 비주얼을 동기화.
        ///
        /// isPasswordEnabled == true:
        ///   - InputField 게임오브젝트 활성화 (비밀번호 입력 가능)
        ///   - off 비주얼 숨김 (체크된 상태로 보임)
        ///
        /// isPasswordEnabled == false:
        ///   - InputField 게임오브젝트 비활성화 (비밀번호 입력 불가)
        ///   - InputField 텍스트 초기화 (의도치 않은 비밀번호 방 생성 방지)
        ///   - off 비주얼 표시 (체크 해제 상태로 보임)
        ///
        /// 왜 interactable이 아닌 SetActive인가:
        ///   UI 스크린샷 기준으로 비밀번호 InputField는 체크박스 OFF 시
        ///   완전히 숨겨진 상태이므로, interactable(회색 처리)이 아니라
        ///   게임오브젝트 자체를 켜고 끄는 것이 실제 UI 동작과 일치한다.
        /// </summary>
        private void ApplyPasswordState()
        {
            // InputField 게임오브젝트 활성/비활성
            if (createRoomPasswordInputField != null)
            {
                createRoomPasswordInputField.gameObject.SetActive(isPasswordEnabled);

                // 비활성화 시 이전 입력 텍스트 제거
                if (!isPasswordEnabled)
                {
                    createRoomPasswordInputField.text = string.Empty;
                }
            }

            // 체크 비주얼: On/Off 오브젝트를 상호 배타적으로 토글
            if (passwordCheckOnVisual != null)
            {
                passwordCheckOnVisual.SetActive(isPasswordEnabled);
            }

            if (passwordCheckOffVisual != null)
            {
                passwordCheckOffVisual.SetActive(!isPasswordEnabled);
            }
        }

        /// <summary>
        /// 비밀번호 체크박스 + InputField를 초기 상태(비활성)로 복원.
        /// </summary>
        private void ResetPasswordCheck()
        {
            isPasswordEnabled = false;
            ApplyPasswordState();
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
            return string.IsNullOrWhiteSpace(raw) ? defaultRoomName : raw.Trim();
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
                SetStatus("Enter a room name first.");
                return;
            }

            var targetRoom = FindRoomByName(roomName);
            if (targetRoom == null)
            {
                SetStatus($"Room not found: {roomName}");
                return;
            }

            var isProtected = NetworkManager.Instance.IsRoomPasswordProtected(targetRoom);
            if (!isProtected)
            {
                NetworkManager.Instance.JoinRoom(targetRoom.Name);
                SetStatus($"Joining room: {targetRoom.Name}");
                return;
            }

            if (joinPasswordPopup != null && joinPasswordPopupInputField != null)
            {
                pendingJoinRoomName = targetRoom.Name;
                joinPasswordPopupInputField.text = string.Empty;
                SetJoinPasswordPopup(true);
                SetStatus("Enter password to join.");
                return;
            }

            var inlinePassword = joinRoomPasswordInputField != null ? joinRoomPasswordInputField.text : string.Empty;
            if (string.IsNullOrWhiteSpace(inlinePassword))
            {
                SetStatus("This room is password protected. Enter password to join.");
                return;
            }

            NetworkManager.Instance.JoinRoom(targetRoom.Name, inlinePassword);
            SetStatus($"Joining room: {targetRoom.Name}");
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
            SetStatus("Joined room.");
            SetCreateRoomPanel(false);
            SetJoinPasswordPopup(false);
            SetSearchRoomPopup(false);
            ClearAllInputFields();
            ResetCreateRoomOptions();
            menuSceneManager?.ShowWaitingRoom();
        }

        private void HandleJoinRoomFailed(short returnCode, string message)
        {
            SetStatus($"Join failed ({returnCode}): {message}");
            Debug.LogWarning($"Join room failed ({returnCode}): {message}");
        }

        private void HandleCreateRoomFailed(short returnCode, string message)
        {
            SetStatus($"Create failed ({returnCode}): {message}");
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
        }

        /// <summary>
        /// 방 생성 옵션(인원수 등)을 기본값으로 초기화.
        ///
        /// 호출 시점:
        ///   - OnEnable (패널 최초 진입)
        ///   - OnClickOpenCreateRoomPopup (팝업 열기)
        ///   - OnClickCloseCreateRoomPopup (팝업 닫기)
        ///   - HandleJoinedRoom (방 진입 성공)
        ///
        /// 왜 ClearCreateRoomInputFields()와 분리하는가:
        ///   SRP — InputField 텍스트 초기화와 선택형 옵션 초기화는 서로 다른 책임이다.
        ///   InputField는 "텍스트를 비운다"이고, 옵션은 "기본값으로 되돌린다"이다.
        ///   추후 난이도, 맵 선택 등 옵션이 추가되면 이 메서드에만 초기화 로직을 넣으면 된다.
        /// </summary>
        private void ResetCreateRoomOptions()
        {
            // --- 인원수 ---
            selectedMaxPlayers = defaultMaxPlayers;

            if (playerCountToggleGroup != null)
            {
                playerCountToggleGroup.allowSwitchOff = false;
            }

            UnbindPlayerCountToggles();
            BindPlayerCountToggles();
            SetPlayerCountToggleWithoutNotify(selectedMaxPlayers);

            // --- 비밀번호 [2] ---
            UnbindPasswordCheckButton();
            ResetPasswordCheck();
            BindPasswordCheckButton();
        }
    }
}
