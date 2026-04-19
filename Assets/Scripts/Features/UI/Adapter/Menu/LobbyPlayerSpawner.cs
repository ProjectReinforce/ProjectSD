using Photon.Pun;
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
    /// </summary>
    public class LobbyPlayerSpawner : MonoBehaviour
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

        /// <summary>
        /// 방에 접속된 상태에서만 호출. 본인 LobbyPlayer가 이미 있으면 no-op.
        /// </summary>
        public void Spawn()
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning("[LobbyPlayerSpawner] 방에 없어서 스폰 불가.");
                return;
            }

            if (localInstance != null) return;

            var pos = ResolveSpawnPosition(PhotonNetwork.LocalPlayer.ActorNumber);
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
