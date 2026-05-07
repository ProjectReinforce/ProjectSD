using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using SwDreams.Shared.Network;
using UnityEngine;
// System.Collections.Hashtable 과 ExitGames.Client.Photon.Hashtable 충돌 해소.
// 이 파일의 모든 Hashtable 사용처는 Photon Custom Properties 용이므로 후자로 고정.
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace SwDreams.Shared.Managers
{
    /// <summary>
    /// Photon 마스터/로비 접속 상태머신.
    ///
    /// Idle ──Connect()──▶ Connecting ──OnJoinedLobby──▶ Connected
    ///                         │
    ///                         ├─OnDisconnected(retryable)──▶ Retrying ──delay──▶ Connecting
    ///                         │                                  │
    ///                         │                                  └─maxRetries──▶ Failed
    ///                         │
    ///                         └─OnDisconnected(non-retryable)──▶ Failed
    /// 인게임 룸 입장 후의 절단(호스트 마이그레이션 등)은 별도 시스템(HostMigrationHandler)이 다룬다.
    /// </summary>
    public enum ConnectionState
    {
        Idle,
        Connecting,
        Retrying,
        Connected,
        Failed
    }

    public class NetworkManager : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        // 플레이어 커스텀 프로퍼티 키
        public const string CharacterIdKey = "characterId";
        public const string IsReadyKey = "isReady";

        // 방 커스텀 프로퍼티 키
        public const string HasPasswordKey = "hasPw";
        public const string PasswordKey = "pw";

        public static NetworkManager Instance { get; private set; }

        [SerializeField] private bool connectOnStart = true;
        [SerializeField] private byte maxPlayersPerRoom = 4;

        [Header("초기 접속 재시도 정책")]
        [Tooltip("자동 재시도 최대 횟수. 첫 시도와는 별도로 카운트(예: 3 → 첫 시도 + 추가 3회).")]
        [SerializeField] private int maxAutoRetries = 3;
        [Tooltip("각 재시도 사이의 대기 시간(초).")]
        [SerializeField] private float retryDelaySeconds = 3f;
        [Tooltip("한 번의 시도가 이 시간 안에 OnConnectedToMaster 까지 못 가면 실패로 간주(초).")]
        [SerializeField] private float connectAttemptTimeoutSeconds = 3f;

        private readonly Dictionary<string, RoomInfo> roomCache = new Dictionary<string, RoomInfo>();
        private bool isCreatingRoom;
        private string pendingJoinPassword = string.Empty;
        private Action pendingMatchmakingAction;
        private bool leavingRoomForMatchmaking;

        // ===== 접속 상태머신 =====
        private Coroutine connectFlowRoutine;
        private int currentRetryAttempt;       // 0 = 첫 시도, 1..maxAutoRetries = 재시도
        private float retryCountdownRemaining; // Retrying 동안 다음 시도까지 남은 초
        private DisconnectCause? lastFailureCause;
        // 사용자가 명시적으로 Disconnect() 한 직후 OnDisconnected 가 자동 재시도로 흐르는 회귀 차단.
        private bool userInitiatedDisconnect;

        public ConnectionState State { get; private set; } = ConnectionState.Idle;
        public int CurrentRetryAttempt => currentRetryAttempt;
        public int MaxRetryAttempts => maxAutoRetries;
        public float RetryCountdownSeconds => retryCountdownRemaining;
        public DisconnectCause? LastFailureCause => lastFailureCause;

        public bool IsConnected => PhotonNetwork.IsConnectedAndReady;
        public bool IsMatchmakingReady => PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InLobby && !PhotonNetwork.InRoom;

        /// <summary>레거시 호환용. true=IsMatchmakingReady, false=그 외. 신규 구독자는 StateChanged 사용.</summary>
        public event Action<bool> ConnectionStateChanged;
        /// <summary>접속 상태 변경 알림. UI 가 문구/버튼 갱신용으로 구독.</summary>
        public event Action<ConnectionState> StateChanged;
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

            // MasterClient의 강퇴 기능(PhotonNetwork.CloseConnection)을 활성화한다.
            // PUN 기본값은 false이며, false 상태에서 CloseConnection을 호출하면
            // "CloseConnection is disabled. No need to call it." 에러만 찍히고 실제 강퇴는 일어나지 않는다.
            // 우리 게임은 호스트 권위 Co-op이므로 앱 시작 시 명시적으로 true로 세팅한다.
            PhotonNetwork.EnableCloseConnection = true;

            // [B8 A-1] AutomaticallySyncScene = false 항상 유지.
            // PUN 의 자동 sync 는 호스트 LoadScene 시 SceneIndex RoomProperty 자동 갱신 → 클라 강제 LoadScene.
            // 이로 인해 다시하기 시 호스트만 대기실 가도 클라가 강제로 따라가는 부작용.
            // 게임 시작은 명시적 RPC_LoadGameScene 으로 모든 클라 동시 LoadScene 처리.
            PhotonNetwork.AutomaticallySyncScene = false;
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
            // 사용자가 다시 Connect() 를 호출했으니 Disconnect 가드 해제.
            userInitiatedDisconnect = false;

            if (PhotonNetwork.IsConnectedAndReady && PhotonNetwork.InLobby)
            {
                SetState(ConnectionState.Connected);
                return;
            }

            if (PhotonNetwork.IsConnected && !PhotonNetwork.IsConnectedAndReady)
            {
                // 연결 진행 중(Master→GameServer 전환 등) — 콜백 대기.
                return;
            }

            // Idle/Failed/Disconnected → 새 접속 사이클 시작.
            currentRetryAttempt = 0;
            lastFailureCause = null;
            StartConnectAttempt();
        }

        /// <summary>
        /// Failed 상태에서 사용자가 누르는 수동 재시도. 자동 재시도 카운터 리셋 후 새 사이클 시작.
        /// </summary>
        public void RetryConnect()
        {
            userInitiatedDisconnect = false;

            if (State == ConnectionState.Connected || State == ConnectionState.Connecting || State == ConnectionState.Retrying)
            {
                return;
            }

            currentRetryAttempt = 0;
            lastFailureCause = null;
            StartConnectAttempt();
        }

        public void Disconnect()
        {
            // 사용자 의도 절단: 자동 재시도 사이클 차단.
            // OnDisconnected 가 곧 발화될 때 이 플래그를 보고 Idle 로 전이만 하고 재시도 사이클을 시작하지 않는다.
            userInitiatedDisconnect = true;
            StopConnectFlow();
            currentRetryAttempt = 0;
            PhotonNetwork.Disconnect();
        }

        private void OnDestroy()
        {
            // 매니저가 어떤 이유로든 파괴될 때 코루틴이 dangling 되지 않도록.
            StopConnectFlow();
        }

        // ===== 상태머신 내부 =====

        private void StartConnectAttempt()
        {
            SetState(ConnectionState.Connecting);

            // 이미 다른 사이클이 돌고 있으면 안전하게 정리.
            StopConnectFlow();

            // 상태별 분기:
            //   완전 끊김    → ConnectUsingSettings (Master 부터)
            //   Master 만 붙음 → JoinLobby
            //   이미 InLobby   → 곧 OnJoinedLobby 호출되거나 워치독 타임아웃
            if (!PhotonNetwork.IsConnected)
            {
                PhotonNetwork.ConnectUsingSettings();
            }
            else if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InLobby)
            {
                PhotonNetwork.JoinLobby();
            }

            // ConnectUsingSettings/JoinLobby 가 동기적으로 OnDisconnected 를 fire 하는 PUN 케이스 방어:
            // 그 콜체인에서 HandleAttemptFailed 가 먼저 돌아 State 가 이미 Retrying/Failed 로 바뀌었을 수 있다.
            // 이 시점에 워치독을 또 시작하면 RetryDelayRoutine 의 connectFlowRoutine reference 를 덮어쓰면서
            // 두 코루틴이 동시에 돌아 currentRetryAttempt 가 가속 증가한다.
            if (State == ConnectionState.Connecting)
            {
                connectFlowRoutine = StartCoroutine(ConnectAttemptWatchdog());
            }
        }

        private IEnumerator ConnectAttemptWatchdog()
        {
            // 시간 측정은 wall-clock(realtimeSinceStartup) 기준.
            // unscaledDeltaTime 누적 방식은 첫 프레임 spike (씬 로딩 직후 dt 가 1초 이상으로 튐) 에 취약하다.
            var startTime = Time.realtimeSinceStartup;
            while (true)
            {
                if (State == ConnectionState.Connected)
                {
                    connectFlowRoutine = null;
                    yield break;
                }
                if (Time.realtimeSinceStartup - startTime >= connectAttemptTimeoutSeconds) break;
                yield return null;
            }

            connectFlowRoutine = null;
            // 시간 안에 못 붙음 → ClientTimeout 으로 실패 처리.
            // OnDisconnected 가 이미 처리했다면 State 가 Retrying/Failed 일 수 있어 가드.
            if (State == ConnectionState.Connecting)
            {
                HandleAttemptFailed(DisconnectCause.ClientTimeout);
            }
        }

        private IEnumerator RetryDelayRoutine()
        {
            // wall-clock 기준 카운트다운. 씬 로딩 직후 unscaledDeltaTime spike 로 카운트가 통째로 깎이는 회귀 방지.
            var deadline = Time.realtimeSinceStartup + retryDelaySeconds;
            retryCountdownRemaining = retryDelaySeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                retryCountdownRemaining = deadline - Time.realtimeSinceStartup;
                yield return null;
            }
            retryCountdownRemaining = 0f;

            connectFlowRoutine = null;
            // 재시도 사이클 진입.
            StartConnectAttempt();
        }

        private void StopConnectFlow()
        {
            if (connectFlowRoutine != null)
            {
                StopCoroutine(connectFlowRoutine);
                connectFlowRoutine = null;
            }
            retryCountdownRemaining = 0f;
        }

        /// <summary>
        /// 한 번의 접속 시도가 실패했을 때 호출.
        /// 재시도 가능한 cause + 잔여 횟수 있음 → Retrying.
        /// 그 외 → Failed.
        /// </summary>
        private void HandleAttemptFailed(DisconnectCause cause)
        {
            lastFailureCause = cause;
            StopConnectFlow();

            if (!IsRetryableCause(cause) || currentRetryAttempt >= maxAutoRetries)
            {
                SetState(ConnectionState.Failed);
                // 워치독 만료 케이스에서 PUN 소켓이 살아있을 수 있다. 깨끗하게 마무리.
                if (PhotonNetwork.IsConnected)
                {
                    PhotonNetwork.Disconnect();
                }
                return;
            }

            currentRetryAttempt++;
            SetState(ConnectionState.Retrying);

            // 워치독 timeout 으로 들어온 경우 PUN 소켓이 살아있을 수 있어,
            // 다음 사이클이 깨끗한 ConnectUsingSettings 분기를 타도록 명시적으로 끊는다.
            // 이 시점 State==Retrying 이므로 곧 발화될 OnDisconnected 는 어느 분기에도 안 걸려 무시된다.
            if (PhotonNetwork.IsConnected)
            {
                PhotonNetwork.Disconnect();
            }

            connectFlowRoutine = StartCoroutine(RetryDelayRoutine());
        }

        private static bool IsRetryableCause(DisconnectCause cause)
        {
            switch (cause)
            {
                // 일시적 네트워크 문제 → 재시도 의미 있음.
                case DisconnectCause.ExceptionOnConnect:
                case DisconnectCause.DnsExceptionOnConnect:
                case DisconnectCause.ServerTimeout:
                case DisconnectCause.ClientTimeout:
                case DisconnectCause.ServerAddressInvalid:
                case DisconnectCause.Exception:
                    return true;

                // 인증/권한 문제 → 재시도해도 안 됨.
                case DisconnectCause.InvalidAuthentication:
                case DisconnectCause.AuthenticationTicketExpired:
                case DisconnectCause.MaxCcuReached:
                case DisconnectCause.InvalidRegion:
                case DisconnectCause.OperationNotAllowedInCurrentState:
                case DisconnectCause.CustomAuthenticationFailed:
                case DisconnectCause.DisconnectByClientLogic:
                case DisconnectCause.DisconnectByServerLogic:
                case DisconnectCause.DisconnectByServerReasonUnknown:
                    return false;

                default:
                    // 알려지지 않은 cause 는 보수적으로 재시도.
                    return true;
            }
        }

        private void SetState(ConnectionState next)
        {
            if (State == next) return;
            State = next;
            StateChanged?.Invoke(next);
            // 레거시 bool 시그니처 호환.
            ConnectionStateChanged?.Invoke(next == ConnectionState.Connected);
        }

        /// <summary>
        /// 로비에 접속되어 있지 않을 때 로비에 재접속.
        /// 이미 로비에 있으면 Photon이 자동으로 방 목록을 푸시하므로 별도 처리 불필요.
        /// </summary>
        public void RefreshRoomList()
        {
            if (!PhotonNetwork.InLobby)
            {
                PhotonNetwork.JoinLobby();
            }
        }

        public void CreateSoloRoom()
        {
            Debug.Log($"[NetworkManager] CreateSoloRoom 호출 — InRoom:{PhotonNetwork.InRoom}, InLobby:{PhotonNetwork.InLobby}");
            RunWhenMatchmakingReady(() =>
            {
                isCreatingRoom = true;
                var roomName = $"Solo_{UnityEngine.Random.Range(1000, 9999)}";
                Debug.Log($"[NetworkManager] CreateRoom 요청: {roomName}");
                // 솔로 방은 로비 방 목록에서 표시하지 않도록 설정.
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
                // 비밀번호 유무/값을 방 커스텀 프로퍼티에 저장해
                // 클라이언트가 입장 전에 비밀번호 입력 필요 여부를 판단할 수 있게 함
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

        /// <summary>
        /// 대상 플레이어를 방에서 강퇴. MasterClient 전용.
        ///
        /// 무엇: PhotonNetwork.CloseConnection(player)로 대상 클라의 연결을 강제 종료 → 서버가 방에서 퇴장시킴.
        /// 왜:   PUN의 표준 강퇴 API. MasterClient만 호출 권한을 갖는다.
        /// 어떻게: 마스터가 아니거나 대상이 null/본인이면 no-op. 성공 시 대상 클라에 OnLeftRoom 발화 → 메뉴 복귀.
        /// </summary>
        public void KickPlayer(Player player)
        {
            if (player == null || player.IsLocal) return;

            if (!PhotonNetwork.IsMasterClient)
            {
                Debug.LogWarning("[NetworkManager] KickPlayer는 MasterClient만 호출 가능합니다.");
                return;
            }

            PhotonNetwork.CloseConnection(player);
            Debug.Log($"[NetworkManager] Kick {player.NickName} (Actor #{player.ActorNumber})");
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

        /// <summary>
        /// 호스트가 게임을 시작할 수 있는지 여부.
        ///
        /// 무엇: 호스트 외 플레이어들이 모두 준비됐는지 체크.
        /// 왜:   대기실 UX에서 호스트 버튼은 "시작" 의미이므로 호스트 자신은 Ready 토글을 누를 수 없다.
        ///       따라서 호스트의 IsReady 여부는 시작 조건에서 제외해야 "전원 준비 → 호스트 시작"이 성립한다.
        ///       호스트 혼자면 루프가 통과되어 true (솔로 시작 가능).
        /// </summary>
        public bool CanMasterStartGameInCurrentRoom()
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            {
                return false;
            }

            var players = PhotonNetwork.PlayerList;
            for (var i = 0; i < players.Length; i++)
            {
                if (players[i].IsMasterClient) continue; // 호스트 본인은 제외
                if (!IsPlayerReady(players[i]))
                {
                    return false;
                }
            }

            return true;
        }


        public override void OnConnectedToMaster()
        {
            // Master 접속만으로는 매치메이킹 준비가 완료되지 않으므로 로비 진입을 먼저 수행한다.
            // 상태는 Connecting 유지 → OnJoinedLobby 에서 Connected 로 전이.
            PhotonNetwork.JoinLobby();
            Debug.Log("Connected to Photon Master.");
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            roomCache.Clear();
            CachedRoomList = Array.Empty<RoomInfo>();
            RoomListChanged?.Invoke();
            Debug.LogWarning($"Disconnected from Photon: {cause}");

            // 사용자가 명시적으로 끊었으면(Disconnect() 호출) 자동 재시도 사이클 진입을 차단.
            // 모든 다른 분기보다 먼저 처리.
            if (userInitiatedDisconnect)
            {
                userInitiatedDisconnect = false;
                SetState(ConnectionState.Idle);
                return;
            }

            // 초기 접속 사이클(Connecting) 중 실패 → 자동 재시도/Failed 분기로.
            if (State == ConnectionState.Connecting)
            {
                HandleAttemptFailed(cause);
                return;
            }

            // Connected 였다가 끊긴 경우(룸 밖) → Idle 로 환원해서 다음 Connect() 시 재진입 가능하게.
            // 룸 안 절단은 호스트 마이그레이션 핸들러 영역이라 상태머신은 손대지 않음.
            if (State == ConnectionState.Connected && !PhotonNetwork.InRoom)
            {
                SetState(ConnectionState.Idle);
            }
        }

        public override void OnJoinedLobby()
        {
            // 로비 진입 = 매치메이킹 준비 완료 = Connected.
            StopConnectFlow();
            currentRetryAttempt = 0;
            lastFailureCause = null;
            SetState(ConnectionState.Connected);
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
            // 비밀번호 방에 입장 직후 검증하고, 불일치 시 즉시 퇴장 처리.
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

            // 비밀번호 방인데 실제 비밀번호 값이 없으면 비정상 방으로 간주.
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
                // 이미 방(게임씬 등)에 있으면 먼저 방을 떠난 후 로비에서 작업 수행.
                pendingMatchmakingAction = action;
                leavingRoomForMatchmaking = true;
                LeaveRoom();
                return;
            }

            if (!PhotonNetwork.InLobby)
            {
                // 로비 콜백(OnJoinedLobby) 이후 수행.
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

        // ===== [B8 A-1] 게임 시작 명시적 동기화 =====

        /// <summary>
        /// 호스트 전용. 모든 클라에 GameScene 로드 신호 송신.
        /// PhotonNetwork.LoadLevel 대신 사용 — autoSync=false 유지하면서도 양쪽 동시 LoadScene 보장.
        /// 다시하기 같은 "호스트만 처리" 흐름은 SceneManager.LoadScene 로 처리해 클라 강제 sync 안 함.
        /// PhotonView 의존을 피해 RaiseEvent 사용 (DropSpawner 와 같은 패턴).
        /// </summary>
        public void RequestLoadGameScene(string sceneName)
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                Debug.LogWarning("[NetworkManager] RequestLoadGameScene 은 호스트 전용.");
                return;
            }
            PhotonNetwork.RaiseEvent(
                LoadSceneEvent.EventCode,
                sceneName,
                new RaiseEventOptions { Receivers = ReceiverGroup.All },
                SendOptions.SendReliable);
        }

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code != LoadSceneEvent.EventCode) return;
            var sceneName = photonEvent.CustomData as string;
            if (string.IsNullOrEmpty(sceneName)) return;
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }
    }
}
