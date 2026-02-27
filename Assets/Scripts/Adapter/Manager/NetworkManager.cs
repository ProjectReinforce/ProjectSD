using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Adapter.Manager
{
    public class NetworkManager : MonoBehaviourPunCallbacks
    {
        // 플레이어 커스텀 프로퍼티 키.
        public const string CharacterIdKey = "characterId";
        public const string IsReadyKey = "isReady";

        // 방 커스텀 프로퍼티 키.
        public const string HasPasswordKey = "hasPw";
        public const string PasswordKey = "pw";

        public static NetworkManager Instance { get; private set; }

        [SerializeField] private bool connectOnStart = true;
        [SerializeField] private byte maxPlayersPerRoom = 4;

        private readonly Dictionary<string, RoomInfo> roomCache = new Dictionary<string, RoomInfo>();
        private bool isCreatingRoom;
        private string pendingJoinPassword = string.Empty;
        private Action pendingMatchmakingAction;
        private bool leavingRoomForMatchmaking;

        public bool IsConnected => PhotonNetwork.IsConnectedAndReady;
        public bool IsMatchmakingReady => PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InLobby && !PhotonNetwork.InRoom;

        public event Action<bool> ConnectionStateChanged;
        public event Action RoomListChanged;
        public event Action JoinedRoom;
        public event Action LeftRoom;
        public event Action PlayersInRoomChanged;
        public event Action<short, string> JoinRoomFailed;
        public event Action<short, string> CreateRoomFailed;

        public RoomInfo[] CachedRoomList { get; private set; } = Array.Empty<RoomInfo>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (connectOnStart)
            {
                Connect();
            }
        }

        public void Connect()
        {
            if (PhotonNetwork.IsConnectedAndReady)
            {
                if (!PhotonNetwork.InLobby)
                {
                    PhotonNetwork.JoinLobby();
                }
                else
                {
                    ConnectionStateChanged?.Invoke(true);
                }
                return;
            }

            if (PhotonNetwork.IsConnected)
            {
                // 연결 진행 중이거나 GameServer 상태면 콜백을 기다린다.
                return;
            }

            PhotonNetwork.AutomaticallySyncScene = true;
            PhotonNetwork.ConnectUsingSettings();
        }

        public void Disconnect()
        {
            PhotonNetwork.Disconnect();
        }

        public void RefreshRoomList()
        {
            if (!PhotonNetwork.InLobby)
            {
                PhotonNetwork.JoinLobby();
            }
        }

        public void CreateSoloRoom()
        {
            RunWhenMatchmakingReady(() =>
            {
                isCreatingRoom = true;
                var roomName = $"Solo_{UnityEngine.Random.Range(1000, 9999)}";
                // 솔로 방은 로비 방 목록에서 노출되지 않도록 설정.
                var options = new RoomOptions
                {
                    MaxPlayers = 1,
                    IsVisible = false,
                    IsOpen = false,
                    CleanupCacheOnLeave = true
                };

                PhotonNetwork.CreateRoom(roomName, options, TypedLobby.Default);
            });
        }

        public void CreateRoom(string roomName, string password = "")
        {
            RunWhenMatchmakingReady(() =>
            {
                if (string.IsNullOrWhiteSpace(roomName))
                {
                    roomName = $"Room_{UnityEngine.Random.Range(1000, 9999)}";
                }

                var hasPassword = !string.IsNullOrWhiteSpace(password);
                // 비밀번호 여부/값을 방 프로퍼티에 저장해
                // 클라이언트가 입장 전에 비밀번호 입력 필요 여부를 판단할 수 있게 함.
                var customProps = new Hashtable
                {
                    [HasPasswordKey] = hasPassword
                };
                if (hasPassword)
                {
                    customProps[PasswordKey] = password.Trim();
                }

                var options = new RoomOptions
                {
                    MaxPlayers = maxPlayersPerRoom,
                    IsVisible = true,
                    IsOpen = true,
                    CleanupCacheOnLeave = true,
                    CustomRoomProperties = customProps,
                    // 로비에는 hasPw만 노출하고, 실제 비밀번호 값은 노출하지 않음.
                    CustomRoomPropertiesForLobby = new[] { HasPasswordKey }
                };

                isCreatingRoom = true;
                PhotonNetwork.CreateRoom(roomName.Trim(), options, TypedLobby.Default);
            });
        }

        public void JoinRoom(string roomName, string password = "")
        {
            RunWhenMatchmakingReady(() =>
            {
                if (string.IsNullOrWhiteSpace(roomName))
                {
                    Debug.LogWarning("Cannot join room: empty room name.");
                    return;
                }

                pendingJoinPassword = password ?? string.Empty;
                PhotonNetwork.JoinRoom(roomName.Trim());
            });
        }

        public void LeaveRoom()
        {
            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.LeaveRoom();
            }
        }

        public void SetLocalCharacter(int characterId)
        {
            if (!PhotonNetwork.InRoom)
            {
                return;
            }

            var props = new Hashtable
            {
                [CharacterIdKey] = characterId
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        public void SetLocalReady(bool isReady)
        {
            if (!PhotonNetwork.InRoom)
            {
                return;
            }

            var props = new Hashtable
            {
                [IsReadyKey] = isReady
            };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        public bool TryGetCharacterId(Player player, out int characterId)
        {
            characterId = -1;
            if (!player.CustomProperties.TryGetValue(CharacterIdKey, out var value))
            {
                return false;
            }

            if (value is int intValue)
            {
                characterId = intValue;
                return true;
            }

            try
            {
                characterId = Convert.ToInt32(value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool IsPlayerReady(Player player)
        {
            if (!player.CustomProperties.TryGetValue(IsReadyKey, out var value))
            {
                return false;
            }

            return value is bool boolValue && boolValue;
        }

        public bool IsRoomPasswordProtected(RoomInfo room)
        {
            if (room == null || room.CustomProperties == null)
            {
                return false;
            }

            if (!room.CustomProperties.TryGetValue(HasPasswordKey, out var value))
            {
                return false;
            }

            return value is bool boolValue && boolValue;
        }

        public bool CanMasterStartGameInCurrentRoom()
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            {
                return false;
            }

            var players = PhotonNetwork.PlayerList;
            if (players.Length <= 1)
            {
                // 솔로(방장 1인)는 즉시 시작 가능.
                return true;
            }

            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player.IsMasterClient)
                {
                    // 방장은 Ready 조건에서 제외.
                    continue;
                }

                if (!IsPlayerReady(player))
                {
                    return false;
                }
            }

            return true;
        }

        public override void OnConnectedToMaster()
        {
            // Master 접속만으로는 매치메이킹 준비 완료가 아니므로 로비 진입을 먼저 수행한다.
            ConnectionStateChanged?.Invoke(false);
            PhotonNetwork.JoinLobby();
            Debug.Log("Connected to Photon Master.");
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            ConnectionStateChanged?.Invoke(false);
            roomCache.Clear();
            CachedRoomList = Array.Empty<RoomInfo>();
            RoomListChanged?.Invoke();
            Debug.LogWarning($"Disconnected from Photon: {cause}");
        }

        public override void OnJoinedLobby()
        {
            ConnectionStateChanged?.Invoke(true);
            Debug.Log("Joined Photon lobby.");
            TryRunPendingMatchmakingAction();
        }

        public override void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            for (var i = 0; i < roomList.Count; i++)
            {
                var room = roomList[i];
                if (room.RemovedFromList)
                {
                    roomCache.Remove(room.Name);
                }
                else
                {
                    roomCache[room.Name] = room;
                }
            }

            CachedRoomList = new RoomInfo[roomCache.Count];
            roomCache.Values.CopyTo(CachedRoomList, 0);
            RoomListChanged?.Invoke();
        }

        public override void OnJoinedRoom()
        {
            // 비밀번호 방은 입장 직후 검증하고, 불일치 시 즉시 퇴장 처리.
            if (!isCreatingRoom && IsCurrentRoomPasswordMismatch())
            {
                LeaveRoom();
                JoinRoomFailed?.Invoke(-1001, "Wrong password.");
                pendingJoinPassword = string.Empty;
                return;
            }

            SetLocalReady(false);
            PlayersInRoomChanged?.Invoke();
            JoinedRoom?.Invoke();

            pendingJoinPassword = string.Empty;
            isCreatingRoom = false;
        }

        public override void OnLeftRoom()
        {
            LeftRoom?.Invoke();

            if (leavingRoomForMatchmaking)
            {
                leavingRoomForMatchmaking = false;
                if (!PhotonNetwork.InLobby)
                {
                    PhotonNetwork.JoinLobby();
                }
                else
                {
                    TryRunPendingMatchmakingAction();
                }
            }
        }

        public override void OnPlayerEnteredRoom(Player newPlayer)
        {
            PlayersInRoomChanged?.Invoke();
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            PlayersInRoomChanged?.Invoke();
        }

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            if (changedProps.ContainsKey(IsReadyKey) || changedProps.ContainsKey(CharacterIdKey))
            {
                PlayersInRoomChanged?.Invoke();
            }
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            pendingJoinPassword = string.Empty;
            JoinRoomFailed?.Invoke(returnCode, message);
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            isCreatingRoom = false;
            CreateRoomFailed?.Invoke(returnCode, message);
        }

        private bool IsCurrentRoomPasswordMismatch()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            {
                return false;
            }

            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(HasPasswordKey, out var hasPwValue))
            {
                return false;
            }

            if (!(hasPwValue is bool hasPassword) || !hasPassword)
            {
                return false;
            }

            // 비밀번호 방인데 실제 비밀번호 메타가 없으면 비정상 방으로 간주.
            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PasswordKey, out var pwValue))
            {
                return true;
            }

            var expectedPassword = pwValue?.ToString() ?? string.Empty;
            return !string.Equals(expectedPassword, pendingJoinPassword, StringComparison.Ordinal);
        }

        private void RunWhenMatchmakingReady(Action action)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("Cannot run matchmaking action: not connected.");
                return;
            }

            if (action == null)
            {
                return;
            }

            if (PhotonNetwork.InRoom)
            {
                // 이미 방(게임서버)에 있으면 먼저 방을 나간 뒤 로비에서 작업 실행.
                pendingMatchmakingAction = action;
                leavingRoomForMatchmaking = true;
                LeaveRoom();
                return;
            }

            if (!PhotonNetwork.InLobby)
            {
                // 로비 콜백(OnJoinedLobby) 이후 실행.
                pendingMatchmakingAction = action;
                PhotonNetwork.JoinLobby();
                return;
            }

            action.Invoke();
        }

        private void TryRunPendingMatchmakingAction()
        {
            if (pendingMatchmakingAction == null || !PhotonNetwork.InLobby || PhotonNetwork.InRoom)
            {
                return;
            }

            var action = pendingMatchmakingAction;
            pendingMatchmakingAction = null;
            action.Invoke();
        }
    }
}
