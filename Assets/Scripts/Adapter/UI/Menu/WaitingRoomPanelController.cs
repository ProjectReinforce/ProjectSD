using System.Collections;
using System.Text;
using Adapter.Manager;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Adapter.UI.Menu
{
    public class WaitingRoomPanelController : MonoBehaviour
    {
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

        private Coroutine countdownRoutine;

        private void OnEnable()
        {
            if (NetworkManager.Instance == null)
            {
                SetStateText("NetworkManager not found.");
                return;
            }

            NetworkManager.Instance.PlayersInRoomChanged += HandlePlayersChanged;
            NetworkManager.Instance.LeftRoom += HandleLeftRoom;

            HandlePlayersChanged();
        }

        private void OnDisable()
        {
            if (NetworkManager.Instance == null)
            {
                return;
            }

            NetworkManager.Instance.PlayersInRoomChanged -= HandlePlayersChanged;
            NetworkManager.Instance.LeftRoom -= HandleLeftRoom;
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
            SetStateText(ready ? "Ready." : "Not Ready.");
            RefreshRoleUi();
        }

        // LobbyPanel의 단일 버튼(ReadyStartBtn)에 연결하는 함수.
        public void OnClickReadyOrStart()
        {
            if (!PhotonNetwork.InRoom || NetworkManager.Instance == null)
            {
                return;
            }

            if (PhotonNetwork.IsMasterClient)
            {
                TryStartByHost();
                return;
            }

            // 클라이언트는 Ready 토글 버튼처럼 동작.
            var currentReady = NetworkManager.Instance.IsPlayerReady(PhotonNetwork.LocalPlayer);
            OnToggleReady(!currentReady);
        }

        // 기존 연결 호환용: ReadyStartBtn이 OnClickStartGame으로 연결돼 있어도 동작하게 유지.
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

            // 카운트다운 중 준비 상태가 바뀌면 카운트다운 취소.
            if (countdownRoutine != null &&
                (NetworkManager.Instance == null || !NetworkManager.Instance.CanMasterStartGameInCurrentRoom()))
            {
                SetStateText("Countdown canceled: readiness changed.");
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

            menuSceneManager?.ShowTitle();
        }

        private void RefreshRoleUi()
        {
            var isMaster = PhotonNetwork.IsMasterClient;

            if (readyToggle != null)
            {
                readyToggle.interactable = !isMaster;
                if (isMaster)
                {
                    readyToggle.isOn = false;
                }
            }

            if (startButton != null)
            {
                // 단일 버튼 운용: 방장은 Start, 클라이언트는 Ready 토글.
                startButton.gameObject.SetActive(true);
                startButton.interactable = isMaster
                    ? NetworkManager.Instance != null && NetworkManager.Instance.CanMasterStartGameInCurrentRoom()
                    : true;
            }

            if (readyStartButtonText != null)
            {
                if (isMaster)
                {
                    readyStartButtonText.text = "Start";
                }
                else
                {
                    var isReady = NetworkManager.Instance != null &&
                                  PhotonNetwork.InRoom &&
                                  NetworkManager.Instance.IsPlayerReady(PhotonNetwork.LocalPlayer);
                    readyStartButtonText.text = isReady ? "Cancel Ready" : "Ready";
                }
            }
        }

        private void StartCountdown()
        {
            if (countdownRoutine != null)
            {
                return;
            }

            countdownRoutine = StartCoroutine(StartGameCountdown());
        }

        private void CancelCountdown()
        {
            if (countdownRoutine == null)
            {
                if (countdownText != null)
                {
                    countdownText.text = string.Empty;
                }
                return;
            }

            StopCoroutine(countdownRoutine);
            countdownRoutine = null;

            if (countdownText != null)
            {
                countdownText.text = string.Empty;
            }
        }

        private IEnumerator StartGameCountdown()
        {
            var remain = Mathf.CeilToInt(startCountdownSeconds);
            while (remain > 0)
            {
                if (countdownText != null)
                {
                    countdownText.text = $"Game starts in {remain}";
                }

                yield return new WaitForSeconds(1f);
                remain--;

                if (!PhotonNetwork.IsMasterClient ||
                    NetworkManager.Instance == null ||
                    !NetworkManager.Instance.CanMasterStartGameInCurrentRoom())
                {
                    countdownRoutine = null;
                    if (countdownText != null)
                    {
                        countdownText.text = string.Empty;
                    }
                    yield break;
                }
            }

            countdownRoutine = null;
            if (countdownText != null)
            {
                countdownText.text = "Starting...";
            }

            TestManager.Instance?.EnterGameSceneByMaster();
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
                var ready = NetworkManager.Instance.IsPlayerReady(player) ? "Ready" : "Not Ready";
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
            if (!PhotonNetwork.IsMasterClient)
            {
                return;
            }

            if (NetworkManager.Instance == null || !NetworkManager.Instance.CanMasterStartGameInCurrentRoom())
            {
                SetStateText("All other players must be Ready.");
                return;
            }

            SetStateText("Starting countdown...");
            StartCountdown();
        }
    }
}
