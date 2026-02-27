using System;
using System.Text;
using Adapter.Manager;
using Photon.Realtime;
using TMPro;
using UnityEngine;

namespace Adapter.UI.Menu
{
    public class RoomListPanelController : MonoBehaviour
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
        [SerializeField] private TMP_Text roomListText;
        [SerializeField] private TMP_Text statusText;

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
        }

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

        // 기존 씬 이벤트 호환용: MakeRoomBtn에서 이 함수를 호출하면 생성 팝업을 연다.
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

            // 비밀번호 팝업이 있으면 팝업으로 입력받고, 없으면 인라인 입력 필드를 사용.
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

        private void HandleRoomListChanged()
        {
            if (roomListText == null)
            {
                return;
            }

            if (NetworkManager.Instance == null)
            {
                roomListText.text = "NetworkManager not found.";
                return;
            }

            var rooms = NetworkManager.Instance.CachedRoomList;
            var search = roomSearchInputField != null ? roomSearchInputField.text?.Trim() : string.Empty;

            var sb = new StringBuilder();
            var count = 0;
            for (var i = 0; i < rooms.Length; i++)
            {
                var room = rooms[i];
                if (!string.IsNullOrWhiteSpace(search) &&
                    room.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var lockMark = NetworkManager.Instance.IsRoomPasswordProtected(room) ? " [PW]" : string.Empty;
                sb.Append(room.Name)
                    .Append(lockMark)
                    .Append(" (")
                    .Append(room.PlayerCount)
                    .Append("/")
                    .Append(room.MaxPlayers)
                    .AppendLine(")");
                count++;
            }

            roomListText.text = count == 0 ? "No rooms available." : sb.ToString();
        }

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
    }
}
