using UnityEngine;
using UnityEngine.InputSystem;
using Photon.Pun;
using Photon.Realtime;

namespace SwDreams.Testing
{
    /// <summary>
    /// Phase 2 테스트 진입점.
    /// Photon 접속 → 방 참가 → 2명 대기 → 호스트가 Space로 게임 시작.
    /// 
    /// 흐름:
    /// 1. 자동으로 Photon 접속 + 방 생성/참가
    /// 2. "대기 중... (1/2)" 로그 출력
    /// 3. 2명 모이면 "Space를 눌러 시작" 로그
    /// 4. 호스트가 Space → 모든 클라이언트에서 Player 스폰 + GameState.Playing
    /// </summary>
    [RequireComponent(typeof(PhotonView))]
    public class Phase2TestEntry : MonoBehaviourPunCallbacks
    {
        [Header("설정")]
        [SerializeField] private string playerPrefabName = "PlayerStub";
        [SerializeField] private int requiredPlayers = 2;

        private bool isJoining = false;
        private bool gameStarted = false;

        private void Start()
        {
            if (PhotonNetwork.IsConnected)
            {
                JoinTestRoom();
            }
            else
            {
                Debug.Log("[TestEntry] Photon 서버 연결 시작...");
                PhotonNetwork.ConnectUsingSettings();
            }
        }

        private void Update()
        {
            if (gameStarted) return;
            if (!PhotonNetwork.InRoom) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            // 호스트만 시작 가능
            // if (PhotonNetwork.IsMasterClient
            //     && PhotonNetwork.CurrentRoom.PlayerCount >= requiredPlayers
            //     && kb.spaceKey.wasPressedThisFrame)
            if (PhotonNetwork.IsMasterClient
                && kb.spaceKey.wasPressedThisFrame)
            {
                photonView.RPC(nameof(RPC_StartGame), RpcTarget.All);
            }
        }

        // === Photon 콜백 ===

        public override void OnConnectedToMaster()
        {
            Debug.Log("[TestEntry] Photon 연결 성공!");
            JoinTestRoom();
        }

        public override void OnJoinedRoom()
        {
            Debug.Log($"[TestEntry] 방 참가 완료! ({PhotonNetwork.CurrentRoom.PlayerCount}/{requiredPlayers})");
            LogWaitingStatus();
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            Debug.Log($"[TestEntry] 플레이어 입장: {newPlayer.NickName} ({PhotonNetwork.CurrentRoom.PlayerCount}/{requiredPlayers})");
            LogWaitingStatus();
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            Debug.Log($"[TestEntry] 플레이어 퇴장: {otherPlayer.NickName} ({PhotonNetwork.CurrentRoom.PlayerCount}/{requiredPlayers})");
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            Debug.LogError($"[TestEntry] 방 생성 실패: {message}");
            isJoining = false;
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            Debug.LogError($"[TestEntry] 연결 끊김: {cause}");
            isJoining = false;
        }

        // === RPC ===

        [PunRPC]
        private void RPC_StartGame()
        {
            if (gameStarted) return;
            gameStarted = true;

            Debug.Log("[TestEntry] === 게임 시작! ===");

            // 플레이어 스폰
            Vector2 spawnPos = new Vector2(Random.Range(-2f, 2f), Random.Range(-2f, 2f));
            PhotonNetwork.Instantiate(playerPrefabName, spawnPos, Quaternion.identity);

            // GameManager를 Playing 상태로
            if (Adapter.Manager.GameManager.Instance != null)
            {
                Adapter.Manager.GameManager.Instance.ChangeState(
                    Adapter.Manager.GameManager.GameState.Playing);
            }
        }

        // === 내부 ===

        private void JoinTestRoom()
        {
            if (isJoining) return;
            isJoining = true;

            RoomOptions options = new RoomOptions
            {
                MaxPlayers = 4,
                IsVisible = false
            };

            PhotonNetwork.JoinOrCreateRoom("Phase2Test", options, TypedLobby.Default);
        }

        private void LogWaitingStatus()
        {
            int current = PhotonNetwork.CurrentRoom.PlayerCount;
            if (current >= requiredPlayers)
            {
                if (PhotonNetwork.IsMasterClient)
                    Debug.Log("[TestEntry] 준비 완료! Space를 눌러 시작하세요.");
                else
                    Debug.Log("[TestEntry] 준비 완료! 호스트가 시작할 때까지 대기...");
            }
            else
            {
                Debug.Log($"[TestEntry] 대기 중... ({current}/{requiredPlayers})");
            }
        }
    }
}
