using System;
using SwDreams.Features.UI.Adapter.Menu;
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
            ClearCreateRoomInputFields();
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
                SetStatus($"Room name already exists: {roomName}");
                return;
            }

            var password = createRoomPasswordInputField != null ? createRoomPasswordInputField.text : string.Empty;
            NetworkManager.Instance.CreateRoom(roomName, password);

            SetStatus(string.IsNullOrWhiteSpace(password)
                ? $"Creating room: {roomName}"
                : $"Creating room: {roomName} (password)");
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
    }
}
