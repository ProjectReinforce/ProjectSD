using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using SwDreams.Shared.Network;
using UnityEngine;

namespace SwDreams.Features.UI.Adapter.Menu
{
    /// <summary>
    /// 대기실 진입/퇴장에 맞춰 로컬 플레이어의 LobbyPlayer를 스폰/파괴하는 매니저.
    ///
    /// 무엇: WaitingRoomPanelController가 활성화될 때 본인 LobbyPlayer를 1회 PhotonNetwork.Instantiate.
    ///      패널이 비활성화되거나 방을 떠날 때 본인 인스턴스만 PhotonNetwork.Destroy.
    /// 왜:   PhotonNetwork.Instantiate는 모든 클라이언트에 네트워크 오브젝트를 브로드캐스트하므로
    ///      각 클라가 자기 것만 Instantiate하면 자동으로 서로의 캐릭터가 복제된다.
    /// 어떻게: SpawnPoint Transform 배열에서 ActorNumber로 슬롯을 배정.
    ///        비어 있으면 Vector3.zero 기준 원형 오프셋으로 폴백.
    ///
    /// [B8] LobbyRefreshEvent 수신 시 자기 LobbyPlayer 재 spawn — 늦게 진입한 클라가
    /// 이전 buffered Instantiate event 를 못 받는 PUN 한계 우회.
    /// </summary>
    public class LobbyPlayerSpawner : MonoBehaviour, IOnEventCallback
    {
        [Header("스폰 설정")]
        [Tooltip("Resources/ 하위의 LobbyPlayer 프리팹 이름 (확장자 제외).")]
        [SerializeField] private string lobbyPlayerPrefabName = "LobbyPlayer";

        [Tooltip("씬 내 스폰 포인트들. ActorNumber % Length로 배정.")]
        [SerializeField] private Transform[] spawnPoints;

        [Tooltip("spawnPoints가 비어있을 때 원형으로 배치할 반경.")]
        [SerializeField] private float fallbackRadius = 1.5f;

        private GameObject localInstance;

        public bool HasLocalInstance => localInstance != null;

        private void OnEnable()
        {
            PhotonNetwork.AddCallbackTarget(this);
        }

        private void OnDisable()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
        }

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code != LobbyRefreshEvent.EventCode) return;
            // 다른 클라가 대기실 진입 → 자기 LobbyPlayer 재 spawn 으로 새 buffered event 송신.
            // 진입자가 이걸 받아 자기 측에 spawn.
            if (localInstance == null) return; // 자기 LobbyPlayer 없으면 신호 무시 (대기실 아닌 상태)

            // 위치 보존 — 사용자가 움직인 위치를 새 spawn 에 그대로 적용.
            Vector3 currentPos = localInstance.transform.position;
            Despawn();
            Spawn(currentPos);
        }

        /// <summary>
        /// 본인 진입을 다른 클라들에게 알림 — 그들이 자기 LobbyPlayer 재 spawn 하면
        /// 새 buffered event 가 본인에게 도달해 서로의 캐릭터가 정상 표시됨.
        /// WaitingRoomPanelController.OnEnable 에서 호출.
        /// </summary>
        public static void RaiseRefreshRequest()
        {
            if (!PhotonNetwork.InRoom) return;
            PhotonNetwork.RaiseEvent(
                LobbyRefreshEvent.EventCode,
                null,
                new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                SendOptions.SendReliable);
        }

        /// <summary>
        /// 방에 접속된 상태에서만 호출. 본인 LobbyPlayer가 이미 있으면 no-op.
        /// </summary>
        public void Spawn(Vector3? overridePosition = null)
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning("[LobbyPlayerSpawner] 방에 없어서 스폰 불가.");
                return;
            }

            if (localInstance != null) return;

            var pos = overridePosition ?? ResolveSpawnPosition(PhotonNetwork.LocalPlayer.ActorNumber);
            localInstance = PhotonNetwork.Instantiate(lobbyPlayerPrefabName, pos, Quaternion.identity);

            if (localInstance == null)
            {
                Debug.LogError($"[LobbyPlayerSpawner] PhotonNetwork.Instantiate 실패: " +
                               $"Resources/{lobbyPlayerPrefabName}.prefab 경로 확인.");
            }
        }

        /// <summary>
        /// 대기실을 나가거나 패널이 꺼질 때 호출. 내 인스턴스만 파괴.
        /// </summary>
        public void Despawn()
        {
            if (localInstance == null) return;

            // 이미 방을 떠났다면 PhotonNetwork.Destroy는 실패 → 로컬 파괴로 폴백.
            if (PhotonNetwork.InRoom)
            {
                PhotonNetwork.Destroy(localInstance);
            }
            else
            {
                Destroy(localInstance);
            }

            localInstance = null;
        }

        private Vector3 ResolveSpawnPosition(int actorNumber)
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                int idx = Mathf.Abs(actorNumber) % spawnPoints.Length;
                var sp = spawnPoints[idx];
                if (sp != null) return sp.position;
            }

            // 폴백: ActorNumber를 각도로 사용해 원형 배치.
            float angle = (actorNumber * 90f) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle) * fallbackRadius, Mathf.Sin(angle) * fallbackRadius, 0f);
        }
    }
}
