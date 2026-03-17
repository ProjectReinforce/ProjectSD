using System.Text;
using Adapter.Manager;
using ExitGames.Client.Photon;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Adapter.UI.Menu
{
    public class WaitingRoomPanelController : MonoBehaviourPunCallbacks
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

        private int displayedCountdown = -1;
        private bool isLoadingGameScene;

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
            }

            NetworkManager.Instance.PlayersInRoomChanged += HandlePlayersChanged;
            NetworkManager.Instance.LeftRoom += HandleLeftRoom;

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
        }

        private void Update()
        {
            UpdateCountdownUiAndStart();
        }

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

        public void OnSelectCharacter(int characterId)
        {
            NetworkManager.Instance?.SetLocalCharacter(characterId);
            SetStateText($"Character changed: {characterId}");
            Debug.Log($"Character changed: {characterId}");
        }

        public void OnToggleReady(bool ready)
        {
            if (NetworkManager.Instance == null || !PhotonNetwork.InRoom)
            {
                return;
            }

            NetworkManager.Instance.SetLocalReady(ready);
            SetStateText(ready ? "준비 완료." : "준비 취소.");
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

        public void OnClickLeaveRoom()
        {
            CancelCountdown();
            NetworkManager.Instance?.LeaveRoom();
        }

        private void HandlePlayersChanged()
        {
            RefreshRoomUi();
            RefreshRoleUi();

            if (NetworkManager.Instance == null) return;

            var canStart = NetworkManager.Instance.CanMasterStartGameInCurrentRoom();

            // 모든 클라이언트: 전원 준비 완료 시 씬 동기화 활성화
            // 호스트의 LoadLevel을 따라갈 준비
            if (canStart)
            {
                PhotonNetwork.AutomaticallySyncScene = true;
            }

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

        private void HandleLeftRoom()
        {
            CancelCountdown();
            if (readyToggle != null)
            {
                readyToggle.isOn = false;
            }

            ShowCountdownText(string.Empty);
            menuSceneManager?.ShowTitle();
        }

        private void RefreshRoleUi()
        {
            if (readyToggle != null)
            {
                readyToggle.interactable = true;
                if (NetworkManager.Instance != null && PhotonNetwork.InRoom)
                {
                    readyToggle.isOn = NetworkManager.Instance.IsPlayerReady(PhotonNetwork.LocalPlayer);
                }
            }

            if (startButton != null)
            {
                startButton.gameObject.SetActive(true);
                startButton.interactable = PhotonNetwork.InRoom;
            }

            if (readyStartButtonText != null)
            {
                var isReady = NetworkManager.Instance != null &&
                              PhotonNetwork.InRoom &&
                              NetworkManager.Instance.IsPlayerReady(PhotonNetwork.LocalPlayer);
                readyStartButtonText.text = isReady ? "준비취소" : "준비";
            }
        }

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

            // 모든 클라이언트: 게임 시작 대비 씬 동기화 활성화
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
