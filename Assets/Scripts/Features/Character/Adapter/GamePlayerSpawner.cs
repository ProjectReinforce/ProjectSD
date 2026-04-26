using Photon.Pun;
using SwDreams.Features.Character.Adapter;
using UnityEngine;
using SwDreams.Shared.Managers;

namespace SwDreams.Features.Character.Adapter
{
    /// <summary>
    /// 정상 플로우 플레이어 스포너.
    /// 대기실에서 선택한 캐릭터 정보를 기반으로 플레이어를 스폰.
    ///
    /// 동작 흐름:
    ///   1. AutomaticallySyncScene = false (결과창 이후 독립 전환 대비)
    ///   2. ready 초기화 (대기실 복귀 시 카운트다운 즉시 시작 방지)
    ///   3. CustomProperties에서 characterId 읽기
    ///   4. PhotonNetwork.Instantiate + instantiationData로 characterId 전달
    ///   5. 호스트: GameState → Playing
    /// </summary>
    public class GamePlayerSpawner : MonoBehaviour
    {
        [SerializeField] private string playerPrefabPath = "Player";
        [SerializeField] private float spawnRadius = 2f;

        [Tooltip("characterId를 찾지 못할 때 사용할 기본 캐릭터 ID")]
        [SerializeField] private int fallbackCharacterId = 0;

        // [B8 안전망] race 로 같은 GameScene 로드 frame 안에 두 번 GamePlayerSpawner 가 활성화되어도
        // 한 번만 spawn 하도록 정적 instance 가드. Awake 에서 첫 instance 만 instance 로 기록하고,
        // Start 에서 instance != this 면 spawn skip. NetworkManager 처럼 Destroy(gameObject) 는
        // 안 함 — 같은 prefab(Managers.prefab) 의 다른 매니저들이 이미 Singleton 가드로 정리되므로
        // 중복 GameObject 자체는 그쪽이 처리.
        private static GamePlayerSpawner instance;

        private void Awake()
        {
            if (instance != null && instance != this) return;
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Start()
        {
            if (instance != this)
            {
                Debug.LogWarning("[B8-DIAG] GamePlayerSpawner 중복 instance — spawn skip.");
                return;
            }

            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning("[GamePlayerSpawner] Photon 방에 접속되지 않았습니다.");
                return;
            }

            // ready 초기화 (대기실 복귀 시 이전 ready 잔존 방지)
            if (NetworkManager.Instance != null)
                NetworkManager.Instance.SetLocalReady(false);

            SpawnLocalPlayer();

            // 호스트가 게임 시작 상태로 전환
            if (PhotonNetwork.IsMasterClient)
            {
                SwDreams.Shared.Managers.GameManager.Instance?.ChangeStateNetwork(
                    SwDreams.Shared.Managers.GameManager.GameState.Playing);
            }
        }

        private void SpawnLocalPlayer()
        {
            // [B8] 중복 스폰 가드 — 이전 라운드의 본인 Player PhotonView 가
            // ResultManager.OnRetry 의 Destroy 보다 늦게 실제 GameObject 정리될 가능성 + 후입장 등
            // 엣지케이스 모두 차단. 기존 PV 가 있으면 신규 spawn 스킵.
            var existing = GameObject.FindGameObjectsWithTag("Player");
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] == null) continue;
                var pv = existing[i].GetComponent<PhotonView>();
                if (pv != null && pv.IsMine)
                {
                    Debug.LogWarning("[GamePlayerSpawner] 본인 Player PV 가 이미 존재 — 신규 spawn 스킵.");
                    return;
                }
            }

            int characterId = GetLocalCharacterId();

            var random2D = Random.insideUnitCircle * spawnRadius;
            var spawnPosition = new Vector3(random2D.x, random2D.y, 0f);

            // instantiationData로 characterId 전달.
            // 모든 클라이언트에서 photonView.InstantiationData[0]으로 접근 가능.
            // → PlayerStub.TryInitializeFromInstantiationData()에서 읽음.
            object[] instantiationData = new object[] { characterId };

            PhotonNetwork.Instantiate(
                playerPrefabPath,
                spawnPosition,
                Quaternion.identity,
                0,
                instantiationData
            );

            Debug.Log($"[GamePlayerSpawner] 플레이어 스폰 (CharacterID: {characterId})");
        }

        /// <summary>
        /// 대기실에서 설정한 characterId를 CustomProperties에서 읽기.
        /// NetworkManager.SetLocalCharacter()로 저장된 값.
        /// </summary>
        private int GetLocalCharacterId()
        {
            if (NetworkManager.Instance != null &&
                NetworkManager.Instance.TryGetCharacterId(PhotonNetwork.LocalPlayer, out int id))
            {
                return id;
            }

            Debug.LogWarning($"[GamePlayerSpawner] characterId를 찾을 수 없습니다. " +
                             $"기본값({fallbackCharacterId}) 사용.");
            return fallbackCharacterId;
        }
    }
}
