using System;
using System.Text;
using Adapter.Manager;
using Photon.Realtime;
using TMPro;
using UnityEngine;

namespace Adapter.UI.Menu
{
    /// <summary>
    /// 방 리스트 패널의 전체 흐름을 관리하는 Controller.
    ///
    /// 변경 사항 (1번 작업):
    ///   - 기존: StringBuilder로 TMP_Text 한 줄에 모든 방 표시
    ///   - 변경: RoomListView를 통해 개별 RoomListItem 프리팹으로 표시
    ///   - IRoomListItemHandler 구현으로 아이템 클릭 이벤트 처리
    ///
    /// 책임(SRP):
    ///   - NetworkManager 이벤트 구독/해제
    ///   - 방 생성/참가 요청 중계
    ///   - 비밀번호 팝업 흐름 관리
    ///   - UI 아이템의 실제 생성/재활용은 RoomListView에 위임
    /// </summary>
    public class RoomListPanelController : MonoBehaviour, IRoomListItemHandler
    {
        [SerializeField] private MenuSceneManager menuSceneManager;
        [SerializeField] private string defaultRoomName = "Room_0001";

        [Header("Create Room UI")]
        [SerializeField] private GameObject makeRoomPanel;
        [SerializeField] private TMP_InputField roomNameInputField;
        [SerializeField] private TMP_InputField createRoomPasswordInputField;

        [Header("Join Room UI")]
        [SerializeField] private TMP_InputField roomSearchInputField;
        [SerializeField] private TMP_InputField joinRoomPasswordInputField;
        [SerializeField] private GameObject joinPasswordPopup;
        [SerializeField] private TMP_InputField joinPasswordPopupInputField;

        [Header("Display")]
        [SerializeField] private RoomListView roomListView;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text emptyListText;

        private string pendingJoinRoomName = string.Empty;

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

            HandleRoomListChanged();
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

            // 패널이 비활성화될 때 아이템 정리
            if (roomListView != null)
            {
                roomListView.ClearAll();
            }
        }

        // ===== IRoomListItemHandler 구현 =====

        /// <summary>
        /// RoomListItem에서 클릭 이벤트가 올라올 때 호출.
        /// 인터페이스를 통해 전달되므로 RoomListItem은 이 Controller를 모른다.
        /// </summary>
        public void OnRoomItemClicked(RoomInfo roomInfo)
        {
            if (roomInfo == null)
            {
                return;
            }

            TryJoinRoom(roomInfo.Name);
        }

        // ===== 버튼 핸들러 =====

        public void OnClickRefreshRoomList()
        {
            NetworkManager.Instance?.RefreshRoomList();
            SetStatus("Refreshing room list...");
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

        public void OnClickJoinRoomFromInput()
        {
            var roomName = ReadJoinRoomName();
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

        // ===== 내부 로직 =====

        private string ReadCreateRoomNameOrDefault()
        {
            var raw = roomNameInputField != null ? roomNameInputField.text : string.Empty;
            return string.IsNullOrWhiteSpace(raw) ? defaultRoomName : raw.Trim();
        }

        private string ReadJoinRoomName()
        {
            var raw = roomSearchInputField != null ? roomSearchInputField.text : string.Empty;
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

            // 비밀번호 팝업
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

        /// <summary>
        /// 방 목록이 변경될 때마다 호출.
        /// 기존: StringBuilder → TMP_Text
        /// 변경: RoomListView.SyncItems()로 아이템 단위 diff 갱신
        /// </summary>
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
            var search = roomSearchInputField != null ? roomSearchInputField.text?.Trim() : string.Empty;

            // RoomListView에 동기화 위임
            // filter: 검색어가 있으면 방 이름에 포함된 것만 표시
            // isPasswordProtected: 자물쇠 아이콘 표시 여부 판단 위임
            roomListView.SyncItems(
                rooms,
                handler: this,
                isPasswordProtected: room => NetworkManager.Instance.IsRoomPasswordProtected(room),
                filter: room =>
                {
                    if (string.IsNullOrWhiteSpace(search))
                    {
                        return true;
                    }

                    return room.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
                });

            // 빈 목록 안내 텍스트
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
    }
}
