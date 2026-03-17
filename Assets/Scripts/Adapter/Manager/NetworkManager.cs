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
        // ?뚮젅?댁뼱 而ㅼ뒪? ?꾨줈?쇳떚 ??
        public const string CharacterIdKey = "characterId";
        public const string IsReadyKey = "isReady";

        // 諛?而ㅼ뒪? ?꾨줈?쇳떚 ??
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
                // ?곌껐 吏꾪뻾 以묒씠嫄곕굹 GameServer ?곹깭硫?肄쒕갚??湲곕떎由곕떎.
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
            Debug.Log($"[NetworkManager] CreateSoloRoom 호출 — InRoom:{PhotonNetwork.InRoom}, InLobby:{PhotonNetwork.InLobby}");
            RunWhenMatchmakingReady(() =>
            {
                isCreatingRoom = true;
                var roomName = $"Solo_{UnityEngine.Random.Range(1000, 9999)}";
                Debug.Log($"[NetworkManager] CreateRoom 요청: {roomName}");
                // ?붾줈 諛⑹? 濡쒕퉬 諛?紐⑸줉?먯꽌 ?몄텧?섏? ?딅룄濡??ㅼ젙.
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
                // 鍮꾨?踰덊샇 ?щ?/媛믪쓣 諛??꾨줈?쇳떚????ν빐
                // ?대씪?댁뼵?멸? ?낆옣 ?꾩뿉 鍮꾨?踰덊샇 ?낅젰 ?꾩슂 ?щ?瑜??먮떒?????덇쾶 ??
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
                    // 濡쒕퉬?먮뒗 hasPw留??몄텧?섍퀬, ?ㅼ젣 鍮꾨?踰덊샇 媛믪? ?몄텧?섏? ?딆쓬.
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
            for (var i = 0; i < players.Length; i++)
            {
                if (!IsPlayerReady(players[i]))
                {
                    return false;
                }
            }

            return true;
        }


        public override void OnConnectedToMaster()
        {
            // Master ?묒냽留뚯쑝濡쒕뒗 留ㅼ튂硫붿씠??以鍮??꾨즺媛 ?꾨땲誘濡?濡쒕퉬 吏꾩엯??癒쇱? ?섑뻾?쒕떎.
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
            // 鍮꾨?踰덊샇 諛⑹? ?낆옣 吏곹썑 寃利앺븯怨? 遺덉씪移???利됱떆 ?댁옣 泥섎━.
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

            // 鍮꾨?踰덊샇 諛⑹씤???ㅼ젣 鍮꾨?踰덊샇 硫뷀?媛 ?놁쑝硫?鍮꾩젙??諛⑹쑝濡?媛꾩＜.
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
                // ?대? 諛?寃뚯엫?쒕쾭)???덉쑝硫?癒쇱? 諛⑹쓣 ?섍컙 ??濡쒕퉬?먯꽌 ?묒뾽 ?ㅽ뻾.
                pendingMatchmakingAction = action;
                leavingRoomForMatchmaking = true;
                LeaveRoom();
                return;
            }

            if (!PhotonNetwork.InLobby)
            {
                // 濡쒕퉬 肄쒕갚(OnJoinedLobby) ?댄썑 ?ㅽ뻾.
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

